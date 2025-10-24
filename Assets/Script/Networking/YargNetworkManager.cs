using Mirror;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace YARG.Networking
{
    /// <summary>
    /// Main network manager for YARG online multiplayer using Mirror.
    /// Handles P2P connections, lobby management, and player state synchronization.
    /// </summary>
    public class YargNetworkManager : NetworkManager
    {
        public static YargNetworkManager Instance { get; private set; }
        
        // Static list to store ordered menu navigation after scene load (e.g., host quitting song)
        private static readonly List<Menu.MenuManager.Menu> _menuNavigationAfterSceneLoad = new();

        [Header("YARG Settings")]
        [SerializeField] private int maxPlayers = 32;
        [SerializeField] private int maxLocalPlayersPerClient = 4;
        [SerializeField] private int maxDisplayedPlayers = 4;

        [Header("Lobby Settings")]
        [SerializeField] private string lobbyName = "YARG Lobby";
        [SerializeField] private LobbyPrivacyMode privacyMode = LobbyPrivacyMode.Public;
        [SerializeField] private string lobbyPassword = "";

        private Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>> _connectedPlayers = new Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>>();
        private LobbyInfo _currentLobby;
        private string _playerName;
        private bool _isHost = false;
        private YARG.Multiplayer.MultiplayerShowPlaylist _multiplayerShowPlaylist;
        private static bool _isQuitting = false;

        private readonly HashSet<uint> _serverGameplayReadyPlayers = new();
        private bool _serverGameplayBarrierActive;
        private double _serverGameplayStartTime;
        private const float GAMEPLAY_START_COUNTDOWN_SECONDS = 0.25f;

        private UniTaskCompletionSource<double> _clientGameplayStartTcs;
        private bool _clientReadyReported;

        public int MaxPlayers => maxPlayers;
        public int MaxLocalPlayersPerClient => maxLocalPlayersPerClient;
        public LobbyInfo CurrentLobby => _currentLobby;
        public string PlayerName => _playerName;
        public bool IsHosting => _isHost;
        public Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>> ConnectedPlayers => _connectedPlayers;
        public bool IsConnected => isNetworkActive && !_isHost;
        public YARG.Multiplayer.MultiplayerShowPlaylist MultiplayerShowPlaylist => _multiplayerShowPlaylist;
        // Events
        public event Action<LobbyInfo> OnLobbyCreated;
        public event Action<LobbyInfo> OnLobbyJoined;
        public event Action OnLobbyLeft;
        public event Action<List<LobbyInfo>> OnLobbyListUpdated;
        public event Action<NetworkPlayerData> OnPlayerJoined;
        public event Action<NetworkPlayerData> OnPlayerLeft;
        public event Action<string> OnNetworkError;
        public event Action<NetworkConnectionToClient> OnClientConnected;
        public event Action<NetworkConnectionToClient> OnClientDisconnected;

        public enum LobbyPrivacyMode
        {
            Public,
            Private,
            FriendsOnly
        }

        public override void Awake()
        {
            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Set default player name (will be updated from profile when network player spawns)
            _playerName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";

            // Configure Mirror settings
            maxConnections = maxPlayers;
            
            // Disable auto-create player - we'll handle spawning manually
            autoCreatePlayer = false;
            
            // Register spawn handler for MultiplayerShowPlaylist
            RegisterMultiplayerShowPlaylistSpawnHandler();
            
            Debug.Log("[YargNetworkManager] Initialized with autoCreatePlayer disabled");
        }

        // Use a consistent hash for the MultiplayerShowPlaylist spawnable
        private const uint PLAYLIST_ASSET_HASH = 0x12345678;

        private void RegisterMultiplayerShowPlaylistSpawnHandler()
        {
            // Mirror clears spawn handlers whenever the client shuts down, so make sure we always re-register.
            NetworkClient.UnregisterSpawnHandler(PLAYLIST_ASSET_HASH);

            NetworkClient.RegisterSpawnHandler(PLAYLIST_ASSET_HASH, SpawnPlaylistHandler, UnspawnPlaylistHandler);
            Debug.Log($"[YargNetworkManager] Registered spawn handlers for MultiplayerShowPlaylist (hash: {PLAYLIST_ASSET_HASH:X})");
        }
        
        private GameObject SpawnPlaylistHandler(SpawnMessage msg)
        {
            Debug.Log($"[YargNetworkManager] Client spawn handler called for MultiplayerShowPlaylist");
            GameObject go = new GameObject("MultiplayerShowPlaylist");
            _multiplayerShowPlaylist = go.AddComponent<YARG.Multiplayer.MultiplayerShowPlaylist>();
            go.AddComponent<NetworkIdentity>();
            DontDestroyOnLoad(go);
            Debug.Log($"[YargNetworkManager] Client created MultiplayerShowPlaylist and stored reference");
            return go;
        }
        
        private void UnspawnPlaylistHandler(GameObject spawned)
        {
            Debug.Log($"[YargNetworkManager] Client unspawn handler called for MultiplayerShowPlaylist");
            
            // Clear the reference when the object is being unspawned
            if (_multiplayerShowPlaylist != null && _multiplayerShowPlaylist.gameObject == spawned)
            {
                Debug.Log($"[YargNetworkManager] Clearing _multiplayerShowPlaylist reference in unspawn handler");
                _multiplayerShowPlaylist = null;
            }
            
            Destroy(spawned);
        }

        public override void Start()
        {
            base.Start();

            // Set up discovery for LAN/P2P
            if (GetComponent<YargNetworkDiscovery>() == null)
            {
                gameObject.AddComponent<YargNetworkDiscovery>();
            }
        }

        /// <summary>
        /// Maximum player name length (matches Steam profile name limit).
        /// </summary>
        public const int MAX_PLAYER_NAME_LENGTH = 32;

        /// <summary>
        /// Get the player name from the first local profile, or use a default.
        /// Called when spawning network player to ensure profiles are loaded.
        /// </summary>
        public string GetPlayerNameFromProfile()
        {
            string name;
            var localPlayers = YARG.Player.PlayerContainer.Players;
            if (localPlayers != null && localPlayers.Count > 0)
            {
                name = localPlayers[0].Profile.Name;
                Debug.Log($"[YargNetworkManager] Using profile name: {name}");
            }
            else
            {
                name = _playerName; // Use the random name generated in Awake
                Debug.Log($"[YargNetworkManager] No local profile found, using default: {name}");
            }

            // Ensure name respects character limit
            if (name.Length > MAX_PLAYER_NAME_LENGTH)
            {
                name = name.Substring(0, MAX_PLAYER_NAME_LENGTH);
                Debug.LogWarning($"[YargNetworkManager] Player name truncated to {MAX_PLAYER_NAME_LENGTH} characters.");
            }

            return name;
        }

        /// <summary>
        /// Set the local player's name.
        /// </summary>
        public void SetPlayerName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning("[YargNetworkManager] Player name cannot be empty, keeping current name.");
                return;
            }

            // Limit to 32 characters (Steam profile name limit)
            if (name.Length > MAX_PLAYER_NAME_LENGTH)
            {
                name = name.Substring(0, MAX_PLAYER_NAME_LENGTH);
                Debug.LogWarning($"[YargNetworkManager] Player name truncated to {MAX_PLAYER_NAME_LENGTH} characters.");
            }

            _playerName = name;
        }

        /// <summary>
        /// Create a new lobby and start hosting.
        /// </summary>
        public LobbyInfo CreateLobby(string lobbyName, int maxPlayers, LobbyPrivacyMode privacyMode, string password = "")
        {
            this.lobbyName = lobbyName;
            this.maxPlayers = Mathf.Clamp(maxPlayers, 1, 32);
            this.maxConnections = this.maxPlayers;
            this.privacyMode = privacyMode;
            this.lobbyPassword = password;

            // Set network address to localhost for local testing
            // (In production, this would be the host's public IP)
            if (string.IsNullOrEmpty(networkAddress) || networkAddress == "localhost")
            {
                networkAddress = "127.0.0.1";
            }

            var transport = GetComponent<TelepathyTransport>();
            string connectionInfo = transport != null ? $"{networkAddress}:{transport.port}" : networkAddress;

            Debug.Log($"[YargNetworkManager] CreateLobby: Starting host on {connectionInfo}");
            Debug.Log($"[YargNetworkManager] CreateLobby: NetworkServer.active before StartHost: {NetworkServer.active}");
            
            _currentLobby = new LobbyInfo
            {
                lobbyId = Guid.NewGuid().ToString(),
                lobbyName = lobbyName,
                hostName = _playerName,
                currentPlayers = 0,
                maxPlayers = this.maxPlayers,
                privacyMode = privacyMode,
                hasPassword = !string.IsNullOrEmpty(password),
                password = password,
                isActive = true,
                ipAddress = networkAddress
            };

            Debug.Log($"[YargNetworkManager] Creating lobby '{lobbyName}' on {connectionInfo}");

            // Start hosting
            StartHost();
            _isHost = true;

            // Start broadcasting lobby for discovery
            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.AdvertiseServer(_currentLobby);
            }

            Debug.Log($"[YargNetworkManager] Lobby created successfully! Connection info: {connectionInfo}");
            Debug.Log($"[YargNetworkManager] CreateLobby: NetworkServer.active after StartHost: {NetworkServer.active}");
            Debug.Log($"[YargNetworkManager] CreateLobby: NetworkServer listening on port: {transport?.port ?? 0}");
            
            // Trigger OnLobbyCreated event (but don't navigate yet)
            OnLobbyCreated?.Invoke(_currentLobby);
            
            // Host will also trigger OnClientConnect which will fire OnLobbyJoined for navigation

            return _currentLobby;
        }

        /// <summary>
        /// Trigger the lobby list updated event (called by discovery component).
        /// </summary>
        public void TriggerLobbyListUpdated(List<LobbyInfo> lobbies)
        {
            OnLobbyListUpdated?.Invoke(lobbies);
        }
        
        /// <summary>
        /// Trigger the lobby joined event (called by LobbyInfoSync after updating lobby info).
        /// </summary>
        public void TriggerLobbyJoinedEvent(LobbyInfo lobby)
        {
            OnLobbyJoined?.Invoke(lobby);
        }

        /// <summary>
        /// Join a lobby by IP address.
        /// </summary>
        public void JoinLobby(string ipAddress, string password = "")
        {
            // Ensure we're not already connected
            if (NetworkClient.isConnected || NetworkServer.active)
            {
                Debug.LogWarning("Already connected. Disconnecting first...");
                if (NetworkClient.isConnected)
                {
                    StopClient();
                }
                if (NetworkServer.active)
                {
                    StopHost();
                }
            }

            // Set the network address BEFORE starting the client
            networkAddress = ipAddress;
            
            Debug.Log($"[YargNetworkManager] JoinLobby: Attempting to connect to {ipAddress}");
            Debug.Log($"[YargNetworkManager] JoinLobby: NetworkClient.active before StartClient: {NetworkClient.active}");
            Debug.Log($"[YargNetworkManager] JoinLobby: NetworkServer.active: {NetworkServer.active}");
            
            // Create placeholder lobby info for clients
            // The actual lobby details should be synced from the server via RPC (TODO)
            _currentLobby = new LobbyInfo
            {
                lobbyId = "client-joining",
                lobbyName = "Connecting...",
                hostName = "Unknown",
                currentPlayers = 0,
                maxPlayers = maxPlayers,
                ipAddress = ipAddress,
                isActive = true
            };
            
            // Also set it on the transport if it's using a different address
            var transport = GetComponent<TelepathyTransport>();
            if (transport != null)
            {
                Debug.Log($"Using Telepathy Transport with port {transport.port}");
            }

            // Store password for validation
            if (!string.IsNullOrEmpty(password))
            {
                lobbyPassword = password;
            }

            Debug.Log($"[YargNetworkManager] Attempting to join lobby at {ipAddress}");
            Debug.Log($"[YargNetworkManager] networkAddress is now set to: {networkAddress}");

            StartClient();
            
            Debug.Log($"[YargNetworkManager] JoinLobby: StartClient() called");
            Debug.Log($"[YargNetworkManager] JoinLobby: NetworkClient.active after StartClient: {NetworkClient.active}");
        }

        /// <summary>
        /// Join a discovered lobby.
        /// </summary>
        public void JoinDiscoveredLobby(LobbyInfo lobby, string password = "")
        {
            if (lobby.hasPassword && lobby.password != password)
            {
                OnNetworkError?.Invoke("Incorrect password");
                return;
            }

            JoinLobby(lobby.ipAddress, password);
            _currentLobby = lobby;
        }

        /// <summary>
        /// Leave the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            Debug.Log($"[YargNetworkManager] LeaveLobby called. _isHost: {_isHost}, _multiplayerShowPlaylist null: {_multiplayerShowPlaylist == null}");
            
            if (_isHost)
            {
                // Cleanup MultiplayerShowPlaylist if it exists
                if (_multiplayerShowPlaylist != null)
                {
                    Debug.Log($"[YargNetworkManager] Destroying MultiplayerShowPlaylist with {_multiplayerShowPlaylist.ShowPlaylist.Count} songs");
                    
                    if (NetworkServer.active)
                    {
                        NetworkServer.Destroy(_multiplayerShowPlaylist.gameObject);
                    }
                    else
                    {
                        Destroy(_multiplayerShowPlaylist.gameObject);
                    }
                    _multiplayerShowPlaylist = null;
                    Debug.Log("[YargNetworkManager] MultiplayerShowPlaylist destroyed and reference cleared");
                }
                
                StopHost();
                _isHost = false;

                // Stop broadcasting
                var discovery = GetComponent<YargNetworkDiscovery>();
                if (discovery != null)
                {
                    discovery.StopDiscovery();
                }
            }
            else
            {
                // Client: reference will be cleared by unspawn handler when StopClient destroys the object
                StopClient();
            }

            _currentLobby = null;
            _connectedPlayers.Clear();

            OnLobbyLeft?.Invoke();
            Debug.Log("Left lobby");
        }

        /// <summary>
        /// Get list of available lobbies on the network.
        /// </summary>
        public void RefreshLobbyList()
        {
            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.StartDiscovery();
            }
        }

        /// <summary>
        /// Update lobby settings (host only).
        /// </summary>
        public void UpdateLobbySettings(string lobbyName, int maxPlayers, LobbyPrivacyMode privacyMode)
        {
            if (!_isHost || _currentLobby == null) return;

            this.lobbyName = lobbyName;
            this.maxPlayers = maxPlayers;
            this.maxConnections = maxPlayers;
            this.privacyMode = privacyMode;

            _currentLobby.lobbyName = lobbyName;
            _currentLobby.maxPlayers = maxPlayers;
            _currentLobby.privacyMode = privacyMode;

            // Update broadcast
            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.AdvertiseServer(_currentLobby);
            }

            Debug.Log($"Lobby updated: {lobbyName}");
        }

        /// <summary>
        /// Get the lobby's connection info for direct connect.
        /// </summary>
        public string GetLobbyConnectionInfo()
        {
            if (!_isHost) return null;

            var transport = GetComponent<TelepathyTransport>();
            if (transport != null)
            {
                return $"{networkAddress}:{transport.port}";
            }

            return networkAddress;
        }

        #region Mirror Callbacks

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[YargNetworkManager] OnStartServer called. _multiplayerShowPlaylist is null: {_multiplayerShowPlaylist == null}");
            
            // Check if MultiplayerShowPlaylist already exists in the scene (DontDestroyOnLoad persistence)
            if (_multiplayerShowPlaylist == null)
            {
                // Try to find existing playlist object that may have persisted
                _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
                
                if (_multiplayerShowPlaylist != null)
                {
                    Debug.Log($"[YargNetworkManager] Found existing MultiplayerShowPlaylist from previous session with {_multiplayerShowPlaylist.ShowPlaylist.Count} songs - CLEARING");
                    _multiplayerShowPlaylist.ShowPlaylist.Clear();
                    // Don't need to call CmdClearShowPlaylist since server hasn't started yet
                }
            }
            
            // Spawn MultiplayerShowPlaylist as a networked object
            if (_multiplayerShowPlaylist == null)
            {
                Debug.Log("[YargNetworkManager] Creating NEW MultiplayerShowPlaylist");
                GameObject playlistGO = new GameObject("MultiplayerShowPlaylist");
                _multiplayerShowPlaylist = playlistGO.AddComponent<YARG.Multiplayer.MultiplayerShowPlaylist>();
                
                // Add NetworkIdentity
                NetworkIdentity netId = playlistGO.AddComponent<NetworkIdentity>();
                
                DontDestroyOnLoad(playlistGO);
                
                // Spawn it on the network with custom hash - clients will use spawn handler
                NetworkServer.Spawn(playlistGO, PLAYLIST_ASSET_HASH);
                
                Debug.Log($"[YargNetworkManager] MultiplayerShowPlaylist spawned with netId: {netId.netId}, assetHash: {PLAYLIST_ASSET_HASH:X}");
            }
            else
            {
                // Playlist exists and is already cleared - just re-spawn it on the network
                Debug.Log($"[YargNetworkManager] Re-using existing MultiplayerShowPlaylist (already cleared)");
                if (!_multiplayerShowPlaylist.netIdentity.isServer)
                {
                    NetworkServer.Spawn(_multiplayerShowPlaylist.gameObject, PLAYLIST_ASSET_HASH);
                }
            }
            
            Debug.Log($"[YargNetworkManager] Server started and ready. Playlist count: {_multiplayerShowPlaylist.ShowPlaylist.Count}");
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            Debug.Log("[YargNetworkManager] Host started (Server + Client)");
            
            // For host, the player will be spawned via OnServerAddPlayer automatically
            // when NetworkClient.AddPlayer() is called by Mirror's host logic
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"[YargNetworkManager] Client started. Connecting to: {networkAddress}");
            
            // Ensure our custom spawn handlers are re-registered after any previous shutdown.
            RegisterMultiplayerShowPlaylistSpawnHandler();

            var transport = GetComponent<TelepathyTransport>();
            if (transport != null)
            {
                Debug.Log($"[YargNetworkManager] Transport port: {transport.port}");
            }
            
            // Try to find the MultiplayerShowPlaylist if it already exists
            // (It may have been spawned before this callback)
            if (_multiplayerShowPlaylist == null)
            {
                StartCoroutine(FindMultiplayerShowPlaylistCoroutine());
            }
        }
        
        private System.Collections.IEnumerator FindMultiplayerShowPlaylistCoroutine()
        {
            // Wait a frame for network objects to be spawned
            yield return null;
            
            Debug.Log("[YargNetworkManager] Searching for MultiplayerShowPlaylist...");
            _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
            
            if (_multiplayerShowPlaylist != null)
            {
                Debug.Log($"[YargNetworkManager] Found MultiplayerShowPlaylist!");
            }
            else
            {
                Debug.LogWarning("[YargNetworkManager] MultiplayerShowPlaylist not found yet");
            }
        }

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);

            // Initialize player list for this connection
            if (!_connectedPlayers.ContainsKey(conn))
            {
                _connectedPlayers[conn] = new List<NetworkPlayerData>();
            }

            // Update lobby player count
            if (_currentLobby != null)
            {
                _currentLobby.currentPlayers = NetworkServer.connections.Count;
            }

            // Log connection details
            string connectionType = conn is LocalConnectionToClient ? "LOCAL (Host)" : $"REMOTE from {conn.address}";
            Debug.Log($"[YargNetworkManager] Server: Client connected! ID={conn.connectionId}, Type={connectionType}, Total connections={NetworkServer.connections.Count}");

            OnClientConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Skip all custom cleanup during application shutdown to avoid triggering menu/UI events
            if (_isQuitting)
            {
                Debug.Log($"[YargNetworkManager] Application is quitting, skipping OnServerDisconnect cleanup for connection {conn.connectionId}");
                base.OnServerDisconnect(conn);
                return;
            }

            // Get player name before removing
            string disconnectedPlayerName = "Unknown";
            if (_connectedPlayers.ContainsKey(conn))
            {
                var players = _connectedPlayers[conn];
                if (players.Count > 0 && players[0] != null)
                {
                    disconnectedPlayerName = players[0].PlayerName;
                }
            }
            
            // Remove all players from this connection
            if (_connectedPlayers.ContainsKey(conn))
            {
                foreach (var player in _connectedPlayers[conn])
                {
                    OnPlayerLeft?.Invoke(player);
                }
                _connectedPlayers.Remove(conn);
            }

            // Update lobby player count
            if (_currentLobby != null)
            {
                _currentLobby.currentPlayers = NetworkServer.connections.Count;
            }
            
            // Show toast notification for player disconnect (except when host disconnects everyone)
            if (NetworkServer.active && conn.connectionId != 0 && !string.IsNullOrEmpty(disconnectedPlayerName))
            {
                // Show toast to all remaining players
                var allPlayers = GetAllPlayers();
                if (allPlayers.Count > 0 && allPlayers[0] != null)
                {
                    allPlayers[0].RpcShowPlayerLeftToast(disconnectedPlayerName);
                }
            }

            OnClientDisconnected?.Invoke(conn);
            base.OnServerDisconnect(conn);
            Debug.Log($"Client disconnected: {conn.connectionId}");
        }

        private bool _hasTriggeredJoinEvent = false;

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            Debug.Log($"[YargNetworkManager] Successfully connected to host at {networkAddress}!");

            // Request player spawning (since autoCreatePlayer is disabled)
            if (!NetworkClient.ready)
            {
                NetworkClient.Ready();
                Debug.Log("[YargNetworkManager] Client ready state set");
            }
            else
            {
                Debug.Log("[YargNetworkManager] Client already ready");
            }

            // Request to add player for this connection (only if we don't have one)
            if (NetworkClient.localPlayer == null)
            {
                NetworkClient.AddPlayer();
                Debug.Log("[YargNetworkManager] Requested player spawn");
            }
            else
            {
                Debug.Log("[YargNetworkManager] Client already has a local player, skipping AddPlayer()");
            }

            // Only trigger OnLobbyJoined event once
            if (!_hasTriggeredJoinEvent)
            {
                _hasTriggeredJoinEvent = true;
                
                // Ensure we have lobby info (should have been set in JoinLobby or CreateLobby)
                if (_currentLobby == null)
                {
                    Debug.LogWarning("[YargNetworkManager] Connected but _currentLobby is null. Creating default lobby info.");
                    _currentLobby = new LobbyInfo
                    {
                        lobbyId = "unknown",
                        lobbyName = "Connected Lobby",
                        hostName = "Unknown Host",
                        currentPlayers = 1,
                        maxPlayers = maxPlayers,
                        ipAddress = networkAddress,
                        isActive = true
                    };
                }
                
                // Update player count
                if (_isHost)
                {
                    _currentLobby.currentPlayers = 1;
                }
                else
                {
                    // For clients, the server should sync this via RPC (TODO)
                    _currentLobby.currentPlayers = 2; // Temporary placeholder
                }
                
                Debug.Log($"[YargNetworkManager] Triggering OnLobbyJoined for: {_currentLobby.lobbyName}");
                OnLobbyJoined?.Invoke(_currentLobby);
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // Skip cleanup during application shutdown to prevent menu navigation errors
            if (_isQuitting)
            {
                Debug.Log("[YargNetworkManager] Application is quitting, skipping OnClientDisconnect cleanup");
                return;
            }

            _currentLobby = null;
            _connectedPlayers.Clear();
            _hasTriggeredJoinEvent = false;

            OnLobbyLeft?.Invoke();
            Debug.Log("Disconnected from host");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Mirror clears spawn handlers on shutdown; prepare for the next connection immediately.
            RegisterMultiplayerShowPlaylistSpawnHandler();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Check if this connection already has a player
            if (conn.identity != null)
            {
                Debug.LogWarning($"[YargNetworkManager] Connection {conn.connectionId} already has a player. Skipping duplicate spawn.");
                return;
            }

            Debug.Log($"[YargNetworkManager] Adding player for connection {conn.connectionId}");

            // Spawn player prefab
            GameObject playerGO = Instantiate(playerPrefab);
            NetworkServer.AddPlayerForConnection(conn, playerGO);

            // Get player data component
            NetworkPlayerData playerData = playerGO.GetComponent<NetworkPlayerData>();
            if (playerData != null)
            {
                // Set player name directly on the server instead of using Command
                // Commands can only be called by the client that owns the object
                playerData.SetPlayerNameServer(_playerName);
                
                // Set host flag (connection 0 is always the host)
                playerData.SetIsHostServer(conn.connectionId == 0);

                // Add to connected players list
                if (_connectedPlayers.ContainsKey(conn))
                {
                    _connectedPlayers[conn].Add(playerData);
                }
                else
                {
                    Debug.LogWarning($"[YargNetworkManager] Connection {conn.connectionId} not found in _connectedPlayers dictionary");
                }

                OnPlayerJoined?.Invoke(playerData);
                Debug.Log($"[YargNetworkManager] Player spawned successfully for connection {conn.connectionId}");
                
                // Show toast notification for player join (except for host joining their own lobby)
                if (conn.connectionId != 0)
                {
                    // Use RPC to show toast to all clients
                    playerData.RpcShowPlayerJoinedToast(_playerName);
                }
                
                // Sync lobby info to the newly joined client (not to host)
                if (_currentLobby != null && conn is not LocalConnectionToClient)
                {
                    Debug.Log($"[YargNetworkManager] Syncing lobby info to client {conn.connectionId}");
                    playerData.TargetSyncLobbyInfo(_currentLobby.lobbyName, _currentLobby.hostName, 
                        _currentLobby.maxPlayers, _currentLobby.hasPassword, (int)_currentLobby.privacyMode);
                }
            }
            else
            {
                Debug.LogError($"[YargNetworkManager] Player prefab does not have NetworkPlayerData component!");
            }
        }

        public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        {
            base.OnServerError(conn, error, reason);
            Debug.LogError($"[YargNetworkManager] Server error on connection {conn.connectionId}: {error} - {reason}");
            OnNetworkError?.Invoke($"Server error: {reason}");
        }

        public override void OnClientError(TransportError error, string reason)
        {
            base.OnClientError(error, reason);
            Debug.LogError($"[YargNetworkManager] Client error: {error} - {reason}");
            Debug.LogError($"[YargNetworkManager] Was trying to connect to: {networkAddress}");
            OnNetworkError?.Invoke($"Client error: {reason}");
        }

        #endregion

        /// <summary>
        /// Get all players across all connections.
        /// </summary>
        public List<NetworkPlayerData> GetAllPlayers()
        {
            var allPlayers = new List<NetworkPlayerData>();

            // Server: use the tracked dictionary
            if (NetworkServer.active)
            {
                foreach (var kvp in _connectedPlayers)
                {
                    foreach (var player in kvp.Value)
                    {
                        if (player != null)
                        {
                            allPlayers.Add(player);
                        }
                    }
                }
            }
            // Client: find all NetworkPlayerData objects in the scene
            else if (NetworkClient.active)
            {
                foreach (var player in FindObjectsOfType<NetworkPlayerData>())
                {
                    if (player != null)
                    {
                        allPlayers.Add(player);
                    }
                }
            }

            // Ensure deterministic ordering so lists line up across calls
            if (allPlayers.Count > 1)
            {
                allPlayers.Sort((a, b) => a.netId.CompareTo(b.netId));
            }

            // Log scene locations for debugging
            if (allPlayers.Count > 0)
            {
                string context = NetworkServer.active ? "Server" : NetworkClient.active ? "Client" : "Offline";
                Debug.Log($"[YargNetworkManager] GetAllPlayers ({context}): Found {allPlayers.Count} players (sorted by netId)");
                foreach (var player in allPlayers)
                {
                    if (player != null)
                    {
                        Debug.Log($"[YargNetworkManager] - {player.PlayerName} (netId: {player.netId}) in scene: {player.gameObject.scene.name}");
                    }
                }
            }

            return allPlayers;
        }

        /// <summary>
        /// Get player count across all connections.
        /// </summary>
        public int GetTotalPlayerCount()
        {
            return GetAllPlayers().Count;
        }

        /// <summary>
        /// Kick a player from the lobby (host only).
        /// </summary>
        public void KickPlayer(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[YargNetworkManager] KickPlayer called but server is not active");
                return;
            }
            
            if (!isNetworkActive || conn == null)
            {
                Debug.LogWarning("[YargNetworkManager] Cannot kick player - invalid connection");
                return;
            }
            
            Debug.Log($"[YargNetworkManager] Kicking player on connection {conn.connectionId}");
            
            // Disconnect the player
            conn.Disconnect();
        }

        /// <summary>
        /// Event fired when host starts song selection. All clients should listen to this.
        /// </summary>
        public event Action OnHostStartedSongSelection;

        /// <summary>
        /// Host calls this to navigate all clients to the music library.
        /// </summary>
        [Server]
        public void StartSongSelection()
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] StartSongSelection called but not host");
                return;
            }
            
            Debug.Log("[YargNetworkManager] Host starting song selection for all clients");
            
            // Tell all players to navigate to music library
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetNavigateToMusicLibrary();
                }
            }
        }

        /// <summary>
        /// Event fired when a song is selected (host or any player).
        /// </summary>
        public event Action<YARG.Core.Song.SongEntry> OnSongSelected;

        /// <summary>
        /// Trigger the OnSongSelected event.
        /// </summary>
        public void TriggerSongSelectedEvent(YARG.Core.Song.SongEntry song)
        {
            Debug.Log($"[YargNetworkManager] Song selected event triggered: {song.Name}");
            OnSongSelected?.Invoke(song);
        }

        /// <summary>
        /// Host calls this when a song is selected in the music library.
        /// Syncs the selection to all clients.
        /// </summary>
        [Server]
        public void SyncSongSelection(YARG.Core.Song.SongEntry song)
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] SyncSongSelection called but not host");
                return;
            }
            
            Debug.Log($"[YargNetworkManager] Syncing song selection to all clients: {song.Name}");
            
            // Tell all players about the selected song
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetSongSelected(song.Hash.ToString());
                }
            }
        }

        /// <summary>
        /// Host calls this to start the multiplayer song for all clients.
        /// Sets CurrentSong and navigates to difficulty select.
        /// </summary>
        [Server]
        public void StartMultiplayerSong(YARG.Core.Song.SongEntry song)
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] StartMultiplayerSong called but not host");
                return;
            }
            
            Debug.Log($"[YargNetworkManager] Starting multiplayer song for all clients: {song.Name}");
            
            // Tell all players to start the song
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetStartMultiplayerSong(song.Hash.ToString());
                }
            }
        }

        /// <summary>
        /// Host calls this to start gameplay for all clients.
        /// Called after all players have selected their difficulty.
        /// </summary>
        [Server]
        public void StartMultiplayerGameplay()
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] StartMultiplayerGameplay called but not host");
                return;
            }
            
            Debug.Log("[YargNetworkManager] Starting gameplay for all clients");
            
            // Tell all players to load gameplay scene using TargetRpc
            // This uses GlobalVariables.LoadScene for proper additive loading
            _serverGameplayBarrierActive = true;
            _serverGameplayReadyPlayers.Clear();
            _serverGameplayStartTime = 0d;
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.SetGameplayReadyServer(false);
                    playerData.TargetStartGameplay();
                }
            }
        }
        
        /// <summary>
        /// Host calls this to restart gameplay for all clients.
        /// Used when host restarts from pause menu.
        /// </summary>
        public void RestartMultiplayerGameplay()
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] RestartMultiplayerGameplay called but not host");
                return;
            }
            
            Debug.Log("[YargNetworkManager] Restarting gameplay for all clients");
            
            // Tell all players to restart gameplay (reload Gameplay scene)
            _serverGameplayBarrierActive = true;
            _serverGameplayReadyPlayers.Clear();
            _serverGameplayStartTime = 0d;
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.SetGameplayReadyServer(false);
                    playerData.TargetRestartGameplay();
                }
            }
        }

        #region Gameplay Start Coordination

        internal void ClientPrepareGameplayStartBarrier()
        {
            if (!isNetworkActive)
            {
                return;
            }

            _clientGameplayStartTcs = new UniTaskCompletionSource<double>();
            _clientReadyReported = false;
        }

        public async UniTask WaitForMultiplayerGameplayStartAsync(CancellationToken token)
        {
            if (!isNetworkActive || !NetworkClient.active)
            {
                return;
            }

            var identity = NetworkClient.localPlayer;
            if (identity == null)
            {
                Debug.LogWarning("[YargNetworkManager] WaitForMultiplayerGameplayStartAsync called with no local player identity.");
                return;
            }

            var playerData = identity.GetComponent<NetworkPlayerData>();
            if (playerData == null)
            {
                Debug.LogWarning("[YargNetworkManager] Local player does not have NetworkPlayerData component.");
                return;
            }

            if (_clientGameplayStartTcs == null)
            {
                _clientGameplayStartTcs = new UniTaskCompletionSource<double>();
            }

            ReportLocalGameplayReadyInternal(playerData, true);

            try
            {
                await _clientGameplayStartTcs.Task.AttachExternalCancellation(token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
        }

        public void ReportLocalGameplayReady(bool ready)
        {
            if (!isNetworkActive || !NetworkClient.active)
            {
                return;
            }

            var identity = NetworkClient.localPlayer;
            if (identity == null)
            {
                return;
            }

            var playerData = identity.GetComponent<NetworkPlayerData>();
            if (playerData == null)
            {
                return;
            }

            ReportLocalGameplayReadyInternal(playerData, ready);
        }

        private void ReportLocalGameplayReadyInternal(NetworkPlayerData playerData, bool ready)
        {
            if (!isNetworkActive || playerData == null)
            {
                return;
            }

            if (ready)
            {
                if (_clientReadyReported)
                {
                    return;
                }

                _clientReadyReported = true;
                playerData.CmdSetGameplayReady(true);
            }
            else
            {
                _clientReadyReported = false;
                playerData.CmdSetGameplayReady(false);
            }
        }

        internal void ClientHandleGameplayStartSignal(double serverTime, float startDelaySeconds)
        {
            if (_clientGameplayStartTcs != null)
            {
                var tcs = _clientGameplayStartTcs;
                _clientGameplayStartTcs = null;

                UniTask.Void(async () =>
                {
                    if (startDelaySeconds > 0f)
                    {
                        await UniTask.Delay(TimeSpan.FromSeconds(startDelaySeconds));
                    }

                    tcs.TrySetResult(serverTime);
                });
            }
        }

        internal void ClientOnRemoteGameplayReadyStateChanged(NetworkPlayerData playerData, bool ready)
        {
            // Currently used for logging/diagnostics. Hook up UI indicators here if needed.
            if (ready)
            {
                Debug.Log($"[YargNetworkManager] {playerData.PlayerName} marked gameplay ready.");
            }
        }

        [Server]
        public void ServerOnPlayerGameplayReadyStateChanged(NetworkPlayerData playerData, bool previous, bool current)
        {
            if (!_serverGameplayBarrierActive)
            {
                return;
            }

            if (playerData == null)
            {
                return;
            }

            if (current)
            {
                _serverGameplayReadyPlayers.Add(playerData.netId);
            }
            else
            {
                _serverGameplayReadyPlayers.Remove(playerData.netId);
            }

            TryCompleteGameplayStartBarrier();
        }

        [Server]
        private void TryCompleteGameplayStartBarrier()
        {
            if (!_serverGameplayBarrierActive)
            {
                return;
            }

            var players = GetAllPlayers();
            if (players.Count == 0)
            {
                return;
            }

            foreach (var player in players)
            {
                if (player == null || !player.GameplayReady)
                {
                    return;
                }
            }

            _serverGameplayBarrierActive = false;
            _serverGameplayStartTime = Mirror.NetworkTime.time;

            Debug.Log("[YargNetworkManager] All players reported gameplay ready. Broadcasting coordinated start signal.");
            foreach (var player in players)
            {
                player.TargetConfirmGameplayStart(_serverGameplayStartTime, GAMEPLAY_START_COUNTDOWN_SECONDS);
            }
        }

        [Server]
        private void ForceCompleteGameplayStartBarrierInternal()
        {
            if (!_serverGameplayBarrierActive)
            {
                return;
            }

            _serverGameplayBarrierActive = false;
            _serverGameplayStartTime = Mirror.NetworkTime.time;

            Debug.LogWarning("[YargNetworkManager] Force completing gameplay start barrier due to timeout.");
            foreach (var player in GetAllPlayers())
            {
                player?.TargetConfirmGameplayStart(_serverGameplayStartTime, GAMEPLAY_START_COUNTDOWN_SECONDS);
            }
        }

        public void ForceCompleteGameplayStartBarrier()
        {
            if (!NetworkServer.active || !_isHost)
            {
                return;
            }

            ForceCompleteGameplayStartBarrierInternal();
        }

        #endregion
        
        /// <summary>
        /// Host calls this to sync practice mode state to all clients.
        /// Used when host toggles practice mode from pause menu.
        /// </summary>
        public void SyncPracticeMode(bool isPractice)
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] SyncPracticeMode called but not host");
                return;
            }
            
            Debug.Log($"[YargNetworkManager] Syncing practice mode to all clients: {isPractice}");
            
            // Tell all players to set practice mode
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetSyncPracticeMode(isPractice);
                }
            }
        }
        
        /// <summary>
        /// Host calls this to quit gameplay and return all players to menu scene.
        /// Used when host quits from pause menu.
        /// </summary>
        public void QuitMultiplayerGameplay()
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] QuitMultiplayerGameplay called but not host");
                return;
            }
            
            Debug.Log("[YargNetworkManager] Quitting gameplay for all clients");
            
            // Tell all players to quit gameplay (load Menu scene)
            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetQuitGameplay();
                }
            }
        }

        /// <summary>
        /// Host calls this from the score screen once all players are ready.
        /// Advances to the next show song or returns everyone to the music library.
        /// </summary>
        public void AdvanceAfterScoreScreen()
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] AdvanceAfterScoreScreen called but not host");
                return;
            }

            var players = GetAllPlayers();
            foreach (var player in players)
            {
                player?.SetReadyStateServer(false);
            }

            bool hasNextSong = GlobalVariables.State.PlayingAShow &&
                               GlobalVariables.State.ShowSongs != null &&
                               GlobalVariables.State.ShowIndex + 1 < GlobalVariables.State.ShowSongs.Count;

            if (hasNextSong)
            {
                GlobalVariables.State.ShowIndex++;
                var nextSong = GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex];
                GlobalVariables.State.CurrentSong = nextSong;

                foreach (var player in players)
                {
                    player?.TargetBeginNextShowSong(nextSong.Hash.ToString(), GlobalVariables.State.ShowIndex);
                }
            }
            else
            {
                GlobalVariables.State.PlayingAShow = false;
                GlobalVariables.State.ShowIndex = 0;

                SetMenuNavigationAfterSceneLoad(Menu.MenuManager.Menu.OnlineMultiplayer,
                    Menu.MenuManager.Menu.LobbyRoom,
                    Menu.MenuManager.Menu.MusicLibrary);

                foreach (var player in players)
                {
                    player?.TargetReturnToMusicLibraryAfterScore();
                }
            }
        }

        /// <summary>
        /// Check if all players are ready (have selected their difficulty).
        /// </summary>
        public bool AreAllPlayersReady()
        {
            var players = GetAllPlayers();
            if (players.Count == 0) return false;
            
            foreach (var playerData in players)
            {
                if (playerData != null && !playerData.IsReady)
                {
                    return false;
                }
            }
            
            return true;
        }

        /// <summary>
        /// Host calls this to navigate all clients to a specific menu.
        /// Used when host navigates back from difficulty select to music library.
        /// </summary>
        public void SyncMenuNavigation(bool popMenu, Menu.MenuManager.Menu targetMenu = Menu.MenuManager.Menu.None)
        {
            if (!NetworkServer.active || !_isHost)
            {
                Debug.LogWarning("[YargNetworkManager] SyncMenuNavigation called but not host");
                return;
            }
            
            string action = popMenu ? "PopMenu" : $"PushMenu({targetMenu})";
            Debug.Log($"[YargNetworkManager] Syncing menu navigation to all clients: {action}");
            
            // Broadcast via all NetworkPlayerData objects (they're spawned and can send RPCs)
            var allPlayers = GetAllPlayers();
            if (allPlayers.Count > 0)
            {
                // Use first player's NetworkPlayerData to broadcast to all clients
                allPlayers[0].RpcNavigateMenu(popMenu, (int)targetMenu);
            }
            else
            {
                Debug.LogWarning("[YargNetworkManager] No NetworkPlayerData objects found to send RPC!");
            }
        }
        
        /// <summary>
        
        /// <summary>
        /// Sets the ordered list of menus to navigate to after the Menu scene loads.
        /// Used when host quits song and wants to restore the multiplayer navigation stack.
        /// </summary>
        public static void SetMenuNavigationAfterSceneLoad(params Menu.MenuManager.Menu[] targetMenus)
        {
            _menuNavigationAfterSceneLoad.Clear();

            if (targetMenus != null)
            {
                foreach (var menu in targetMenus)
                {
                    if (menu == Menu.MenuManager.Menu.None)
                    {
                        continue;
                    }

                    _menuNavigationAfterSceneLoad.Add(menu);
                }
            }

            var route = _menuNavigationAfterSceneLoad.Count > 0
                ? string.Join(" > ", _menuNavigationAfterSceneLoad)
                : "None";

            Debug.Log($"[YargNetworkManager] Set menu navigation after scene load: {route}");
        }

        /// <summary>
        /// Gets and clears the ordered menu navigation list set by SetMenuNavigationAfterSceneLoad.
        /// Called by MenuManager after Menu scene loads.
        /// </summary>
        public static List<Menu.MenuManager.Menu> GetAndClearMenuNavigationAfterSceneLoad()
        {
            if (_menuNavigationAfterSceneLoad.Count == 0)
            {
                return new List<Menu.MenuManager.Menu>();
            }

            var result = new List<Menu.MenuManager.Menu>(_menuNavigationAfterSceneLoad);
            _menuNavigationAfterSceneLoad.Clear();
            return result;
        }

        /// <summary>
        /// Information about a lobby.
        /// </summary>
        [Serializable]
        public class LobbyInfo
        {
            public string lobbyId;
            public string lobbyName;
            public string hostName;
            public string ipAddress;
            public int currentPlayers;
            public int maxPlayers;
            public LobbyPrivacyMode privacyMode;
            public bool hasPassword;
            public string password;
            public bool isActive;
            public long lastSeen;

            public override string ToString()
            {
                return $"{lobbyName} ({currentPlayers}/{maxPlayers}) - Host: {hostName}";
            }
        }

        public override void OnApplicationQuit()
        {
            _isQuitting = true;
            Debug.Log("[YargNetworkManager] OnApplicationQuit called, setting _isQuitting = true");
            base.OnApplicationQuit();
        }

        private void OnDestroy()
        {
            _isQuitting = true;
            Debug.Log("[YargNetworkManager] OnDestroy called, setting _isQuitting = true");
        }
    }
}