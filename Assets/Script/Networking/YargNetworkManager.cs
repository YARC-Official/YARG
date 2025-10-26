using Mirror;
using kcp2k;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Networking.Bookmarks;
using YARG.Networking.STUN;
using YARG.Networking.UPnP;
using YARG.Core.Logging;
using YARG.Player;
using YARG.Core.Game;
using YARG.Core;
using YARG;

namespace YARG.Networking
{
    /// <summary>
    /// Main network manager for YARG online multiplayer using Mirror.
    /// Handles P2P connections, lobby management, and player state synchronization.
    /// </summary>
    [RequireComponent(typeof(KcpTransport))]
    [DefaultExecutionOrder(-500)]
    public class YargNetworkManager : NetworkManager
    {
        public static YargNetworkManager Instance { get; private set; }

        private static void LogInfo(string message) => YargLogger.LogInfo(message);
        private static void LogWarning(string message) => YargLogger.LogWarning(message);
        private static void LogError(string message) => YargLogger.LogError(message);

        private const uint KcpLowLatencyIntervalMs = 2;
        private const int KcpLowLatencyTimeoutMs = 8000;
        private const uint KcpLowLatencyMinWindow = 2048;
        
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
        [SerializeField] private bool enableAutomaticPortMapping = true;

        private Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>> _connectedPlayers = new Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>>();
        private LobbyInfo _currentLobby;
        private string _playerName;
        private bool _isHost = false;
        private YARG.Multiplayer.MultiplayerShowPlaylist _multiplayerShowPlaylist;
        private static bool _isQuitting = false;
        private NatTraversalService _natService;
        private string _lastJoinAddress;
        private int _lastJoinPort;
        private string _lastJoinPassword;
        private string _lastJoinDisplayName;
        private UpnpPortMapper _upnpMapper;
        private UpnpPortMappingHandle _tcpPortMapping;
        private UpnpPortMappingHandle _udpPortMapping;
        private CancellationTokenSource _portMappingCts;
        private readonly Dictionary<string, DateTime> _recentNatPunches = new();
        private bool _clientJoinPending;
        private bool _localSlotSyncPending;
        private static readonly TimeSpan NatPunchCacheDuration = TimeSpan.FromSeconds(5);
        private IPEndPoint _lastPublicEndpoint;

        private readonly HashSet<uint> _serverGameplayReadyPlayers = new();
        private bool _serverGameplayBarrierActive;
        private double _serverGameplayStartTime;
        private const float GAMEPLAY_START_COUNTDOWN_SECONDS = 0.25f;

        private readonly HashSet<uint> _serverFailedPlayers = new();
        private bool _serverBandFailureTriggered;

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
        public int DefaultPort => ResolveTransportPort();
        public int SuggestedDirectConnectPort => _lastPublicEndpoint != null && _lastPublicEndpoint.Port > 0
            ? _lastPublicEndpoint.Port
            : ResolveTransportPort();
        public IPEndPoint LastPublicEndpoint => _lastPublicEndpoint;
        public bool IsJoinInProgress => _clientJoinPending;
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
            var kcpTransport = GetComponent<KcpTransport>();
            if (kcpTransport == null)
            {
                kcpTransport = gameObject.AddComponent<KcpTransport>();
            }

            ApplyLowLatencyKcpPreset(kcpTransport);

            if (kcpTransport.Port == 0)
            {
                kcpTransport.Port = NetworkTransportDefaults.DefaultUdpPort;
            }

            if (kcpTransport.enabled == false)
            {
                kcpTransport.enabled = true;
            }

            transport = kcpTransport;
            Transport.active = kcpTransport;

            if (TryGetComponent<TelepathyTransport>(out var telepathyTransport))
            {
                if (telepathyTransport.enabled)
                {
                    telepathyTransport.enabled = false;
                }

                if (Application.isPlaying)
                {
                    Destroy(telepathyTransport);
                }
                else
                {
                    DestroyImmediate(telepathyTransport);
                }
            }

            base.Awake();

            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _natService = GetComponent<NatTraversalService>();
            if (_natService == null)
            {
                _natService = NatTraversalService.Instance ?? gameObject.AddComponent<NatTraversalService>();
            }
            else if (NatTraversalService.Instance != null)
            {
                _natService = NatTraversalService.Instance;
            }
            if (_natService != null)
            {
                _natService.PunchPacketReceived -= HandleNatPunchPacket;
                _natService.PunchPacketReceived += HandleNatPunchPacket;
                _natService.PublicEndpointChanged -= HandlePublicEndpointChanged;
                _natService.PublicEndpointChanged += HandlePublicEndpointChanged;
                _natService.AttachTransport(kcpTransport);
                if (_natService.CachedResult != null)
                {
                    HandlePublicEndpointChanged(_natService.CachedResult);
                }
            }

            // Set default player name (will be updated from profile when network player spawns)
            _playerName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";

            // Configure Mirror settings
            maxConnections = maxPlayers;
            
            // Disable auto-create player - we'll handle spawning manually
            autoCreatePlayer = false;
            
            // Register spawn handler for MultiplayerShowPlaylist
            RegisterMultiplayerShowPlaylistSpawnHandler();
            
            LogInfo("[YargNetworkManager] Initialized with autoCreatePlayer disabled");

            PlayerContainer.PlayerAdded += OnLocalPlayerAddedToContainer;
            PlayerContainer.PlayerRemoved += OnLocalPlayerRemovedFromContainer;
        }
        // Use a consistent hash for the MultiplayerShowPlaylist spawnable
        private const uint PLAYLIST_ASSET_HASH = 0x12345678;

        private void RegisterMultiplayerShowPlaylistSpawnHandler()
        {
            // Mirror clears spawn handlers whenever the client shuts down, so make sure we always re-register.
            NetworkClient.UnregisterSpawnHandler(PLAYLIST_ASSET_HASH);

            NetworkClient.RegisterSpawnHandler(PLAYLIST_ASSET_HASH, SpawnPlaylistHandler, UnspawnPlaylistHandler);
            LogInfo($"[YargNetworkManager] Registered spawn handlers for MultiplayerShowPlaylist (hash: {PLAYLIST_ASSET_HASH:X})");
        }

        private static void ApplyLowLatencyKcpPreset(KcpTransport transport)
        {
            if (transport == null)
            {
                return;
            }

            transport.NoDelay = true;

            if (transport.Interval > KcpLowLatencyIntervalMs)
            {
                transport.Interval = KcpLowLatencyIntervalMs;
            }

            if (transport.Timeout > KcpLowLatencyTimeoutMs)
            {
                transport.Timeout = KcpLowLatencyTimeoutMs;
            }

            if (transport.FastResend < 2)
            {
                transport.FastResend = 2;
            }

            if (transport.ReceiveWindowSize < KcpLowLatencyMinWindow)
            {
                transport.ReceiveWindowSize = KcpLowLatencyMinWindow;
            }

            if (transport.SendWindowSize < KcpLowLatencyMinWindow)
            {
                transport.SendWindowSize = KcpLowLatencyMinWindow;
            }

            transport.MaximizeSocketBuffers = true;
        }
        
        private GameObject SpawnPlaylistHandler(SpawnMessage msg)
        {
            LogInfo($"[YargNetworkManager] Client spawn handler called for MultiplayerShowPlaylist");
            GameObject go = new GameObject("MultiplayerShowPlaylist");
            _multiplayerShowPlaylist = go.AddComponent<YARG.Multiplayer.MultiplayerShowPlaylist>();
            go.AddComponent<NetworkIdentity>();
            DontDestroyOnLoad(go);
            LogInfo($"[YargNetworkManager] Client created MultiplayerShowPlaylist and stored reference");
            return go;
        }
        
        private void UnspawnPlaylistHandler(GameObject spawned)
        {
            LogInfo($"[YargNetworkManager] Client unspawn handler called for MultiplayerShowPlaylist");
            
            // Clear the reference when the object is being unspawned
            if (_multiplayerShowPlaylist != null && _multiplayerShowPlaylist.gameObject == spawned)
            {
                LogInfo($"[YargNetworkManager] Clearing _multiplayerShowPlaylist reference in unspawn handler");
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
        public string GetPlayerNameFromProfile(int localIndex = 0)
        {
            string name;
            var localPlayers = PlayerContainer.Players;
            if (localPlayers != null && localPlayers.Count > 0)
            {
                if (localIndex >= 0 && localIndex < localPlayers.Count)
                {
                    name = localPlayers[localIndex].Profile.Name;
                    LogInfo($"[YargNetworkManager] Using profile name: {name} (index {localIndex})");
                }
                else
                {
                    name = _playerName;
                    LogWarning($"[YargNetworkManager] Requested player name for invalid local index {localIndex}. Falling back to default name: {name}");
                }
            }
            else
            {
                name = _playerName; // Use the random name generated in Awake
                LogInfo($"[YargNetworkManager] No local profile found, using default: {name}");
            }

            // Ensure name respects character limit
            if (name.Length > MAX_PLAYER_NAME_LENGTH)
            {
                name = name.Substring(0, MAX_PLAYER_NAME_LENGTH);
                LogWarning($"[YargNetworkManager] Player name truncated to {MAX_PLAYER_NAME_LENGTH} characters.");
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
                LogWarning("[YargNetworkManager] Player name cannot be empty, keeping current name.");
                return;
            }

            // Limit to 32 characters (Steam profile name limit)
            if (name.Length > MAX_PLAYER_NAME_LENGTH)
            {
                name = name.Substring(0, MAX_PLAYER_NAME_LENGTH);
                LogWarning($"[YargNetworkManager] Player name truncated to {MAX_PLAYER_NAME_LENGTH} characters.");
            }

            _playerName = name;
        }

        private void OnLocalPlayerAddedToContainer(YargPlayer player)
        {
            ScheduleLocalPlayerSlotSync();
        }

        private void OnLocalPlayerRemovedFromContainer(YargPlayer player)
        {
            ScheduleLocalPlayerSlotSync();
        }

        private void ScheduleLocalPlayerSlotSync()
        {
            if (_localSlotSyncPending || !isNetworkActive || !NetworkClient.active)
            {
                return;
            }

            _localSlotSyncPending = true;
            SyncLocalPlayerSlotsAsync().Forget();
        }

        internal void OnLocalNetworkPlayerReady(NetworkPlayerData playerData)
        {
            if (playerData == null || !playerData.IsLocalUser)
            {
                return;
            }

            ScheduleLocalPlayerSlotSync();
        }

        private async UniTaskVoid SyncLocalPlayerSlotsAsync()
        {
            var destroyToken = this.GetCancellationTokenOnDestroy();

            try
            {
                const int maxAttempts = 5;
                for (int attempt = 0; attempt < maxAttempts; attempt++)
                {
                    if (TrySendLocalPlayerSlotSync())
                    {
                        break;
                    }

                    await UniTask.Delay(TimeSpan.FromMilliseconds(100), cancellationToken: destroyToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation when object is destroyed.
            }
            finally
            {
                _localSlotSyncPending = false;
            }
        }

        private bool TrySendLocalPlayerSlotSync()
        {
            if (!isNetworkActive || !NetworkClient.active)
            {
                return true;
            }

            var localPlayers = PlayerContainer.Players;
            if (localPlayers == null)
            {
                return false;
            }

            int desiredCount = Mathf.Clamp(localPlayers.Count, 0, maxLocalPlayersPerClient);

            var ownedPlayers = GetAllPlayers()
                .Where(p => p != null && p.IsLocalUser)
                .OrderBy(p => p.PlayerIndex)
                .ToList();

            if (ownedPlayers.Count == 0)
            {
                return false;
            }

            var driver = ownedPlayers[0];

            string[] names = new string[desiredCount];
            int[] instruments = new int[desiredCount];
            int[] difficulties = new int[desiredCount];

            for (int i = 0; i < desiredCount; i++)
            {
                var localPlayer = localPlayers[i];
                var profile = localPlayer?.Profile;

                names[i] = profile?.Name ?? _playerName;
                instruments[i] = (int)(profile?.CurrentInstrument ?? Instrument.FiveFretGuitar);
                difficulties[i] = (int)(profile?.CurrentDifficulty ?? Difficulty.Expert);
            }

            driver.CmdSyncLocalPlayerSlots(names, instruments, difficulties);
            return true;
        }

        [Server]
        internal void ServerSyncLocalPlayerSlots(NetworkConnectionToClient conn, string[] playerNames, int[] instruments, int[] difficulties)
        {
            if (!NetworkServer.active || conn == null)
            {
                return;
            }

            if (!_connectedPlayers.TryGetValue(conn, out var playersForConnection) || playersForConnection == null)
            {
                playersForConnection = new List<NetworkPlayerData>();
                _connectedPlayers[conn] = playersForConnection;
            }

            // Ensure the primary player (conn.identity) is tracked first if available.
            if (conn.identity != null && conn.identity.TryGetComponent(out NetworkPlayerData primaryData))
            {
                if (!playersForConnection.Contains(primaryData))
                {
                    playersForConnection.Insert(0, primaryData);
                }
            }

            playersForConnection.RemoveAll(p => p == null);

            int desiredCount = Mathf.Clamp(playerNames?.Length ?? 0, 0, maxLocalPlayersPerClient);
            int minimumCount = Mathf.Max(1, desiredCount);

            while (playersForConnection.Count < minimumCount)
            {
                if (playerPrefab == null)
                {
                    LogError("[YargNetworkManager] Cannot spawn additional player - playerPrefab is null");
                    break;
                }

                GameObject playerGO = Instantiate(playerPrefab);
                if (!playerGO.TryGetComponent(out NetworkPlayerData additionalData))
                {
                    LogError("[YargNetworkManager] Additional player prefab does not contain NetworkPlayerData component!");
                    Destroy(playerGO);
                    break;
                }

                NetworkServer.Spawn(playerGO, conn);
                additionalData.SetIsHostServer(conn.connectionId == 0);
                playersForConnection.Add(additionalData);
                OnPlayerJoined?.Invoke(additionalData);
            }

            while (playersForConnection.Count > minimumCount)
            {
                int lastIndex = playersForConnection.Count - 1;
                var removed = playersForConnection[lastIndex];
                playersForConnection.RemoveAt(lastIndex);

                if (removed == null)
                {
                    continue;
                }

                OnPlayerLeft?.Invoke(removed);
                NetworkServer.Destroy(removed.gameObject);
            }

            for (int i = 0; i < playersForConnection.Count; i++)
            {
                var data = playersForConnection[i];
                if (data == null)
                {
                    continue;
                }

                data.SetPlayerIndexServer(i);

                if (playerNames != null && i < playerNames.Length && !string.IsNullOrWhiteSpace(playerNames[i]))
                {
                    data.SetPlayerNameServer(playerNames[i]);
                }

                if (instruments != null && difficulties != null && i < instruments.Length && i < difficulties.Length)
                {
                    data.SetInstrumentServer(instruments[i], difficulties[i]);
                }
            }

            if (desiredCount == 0 && playersForConnection.Count > 0)
            {
                var primary = playersForConnection[0];
                if (primary != null)
                {
                    primary.SetPlayerNameServer(_playerName);
                    primary.SetInstrumentServer((int)Instrument.FiveFretGuitar, (int)Difficulty.Expert);
                    primary.SetReadyStateServer(false);
                }
            }

            if (_currentLobby != null)
            {
                _currentLobby.currentPlayers = GetTotalPlayerCount();
            }
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

            // Choose a sensible LAN address when the inspector value is blank/loopback so STUN/UPnP work.
            if (ShouldResolveLocalAddress(networkAddress))
            {
                var resolved = TryGetLocalLanAddress();
                if (!string.IsNullOrEmpty(resolved))
                {
                    LogInfo($"[YargNetworkManager] Using local LAN address '{resolved}' for hosting.");
                    networkAddress = resolved;
                }
                else
                {
                    networkAddress = "127.0.0.1";
                    LogWarning("[YargNetworkManager] Failed to detect LAN address, defaulting to 127.0.0.1.");
                }
            }

            string connectionInfo = $"{networkAddress}:{ResolveTransportPort()}";

            ConfigureNatPunchPort((ushort)ResolveTransportPort());

            LogInfo($"[YargNetworkManager] CreateLobby: Starting host on {connectionInfo}");
            LogInfo($"[YargNetworkManager] CreateLobby: NetworkServer.active before StartHost: {NetworkServer.active}");
            
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
                ipAddress = networkAddress,
                port = ResolveTransportPort(),
                publicPort = ResolveTransportPort(),
                punchPort = _natService != null ? _natService.PunchPort : NetworkTransportDefaults.DefaultUdpPort,
                publicAddress = networkAddress,
                natType = NetworkNatType.Unknown,
                supportsNatTraversal = false,
                transportId = Transport.active != null ? Transport.active.GetType().Name : "Unknown",
                stunServer = string.Empty
            };

            LogInfo($"[YargNetworkManager] Creating lobby '{lobbyName}' on {connectionInfo}");

            // Start hosting
            StartHost();
            _isHost = true;

            // Start broadcasting lobby for discovery
            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.AdvertiseServer(_currentLobby);
            }

            LogInfo($"[YargNetworkManager] Lobby created successfully! Connection info: {connectionInfo}");
            LogInfo($"[YargNetworkManager] CreateLobby: NetworkServer.active after StartHost: {NetworkServer.active}");
            LogInfo($"[YargNetworkManager] CreateLobby: NetworkServer listening on port: {ResolveTransportPort()}");
            
            // Trigger OnLobbyCreated event (but don't navigate yet)
            OnLobbyCreated?.Invoke(_currentLobby);
            
            // Host will also trigger OnClientConnect which will fire OnLobbyJoined for navigation

            ScheduleNatProbe(_currentLobby);
            TrySetupPortMapping(ResolveTransportPort());

            LobbyBookmarkStore.Instance.RecordConnection(
                networkAddress,
                ResolveTransportPort(),
                _currentLobby.lobbyName,
                string.Empty);

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
            if (!_isHost && !string.IsNullOrWhiteSpace(_lastJoinAddress))
            {
                string displayName = !string.IsNullOrWhiteSpace(lobby?.lobbyName)
                    ? lobby.lobbyName
                    : (_lastJoinDisplayName ?? _lastJoinAddress);

                int port = _lastJoinPort != 0 ? _lastJoinPort : ResolveTransportPort();
                LobbyBookmarkStore.Instance.RecordConnection(
                    _lastJoinAddress,
                    port,
                    displayName,
                    _lastJoinPassword ?? string.Empty);

                _lastJoinPassword = string.Empty;
            }

            OnLobbyJoined?.Invoke(lobby);
        }

        /// <summary>
        /// Join a lobby by IP address.
        /// </summary>
        public void JoinLobby(string endpoint, string password = "")
        {
            JoinLobbyAsync(endpoint, password).Forget();
        }

        private async UniTaskVoid JoinLobbyAsync(string endpoint, string password)
        {
            var destroyToken = this.GetCancellationTokenOnDestroy();

            if (_clientJoinPending)
            {
                LogWarning("[YargNetworkManager] Join attempt ignored, another join is already in progress.");
                return;
            }

            bool startClientCalled = false;
            bool punchStarted = false;

            try
            {
                _clientJoinPending = true;

                if (NetworkClient.isConnected || NetworkServer.active)
                {
                    LogWarning("Already connected. Disconnecting first...");
                    if (NetworkClient.isConnected)
                    {
                        StopClient();
                    }
                    if (NetworkServer.active)
                    {
                        StopHost();
                    }
                    await UniTask.NextFrame(destroyToken);
                }

                if (!NetworkClient.isConnected && NetworkClient.active && !NetworkServer.active)
                {
                    LogWarning("[YargNetworkManager] Previous client connection still active. Stopping client before reconnect attempt.");
                    StopClient();
                    await UniTask.NextFrame(destroyToken);
                }

                ParseEndpoint(endpoint, out var address, out var port);
                if (string.IsNullOrWhiteSpace(address))
                {
                    LogError("[YargNetworkManager] JoinLobby failed: endpoint missing address");
                    return;
                }

                networkAddress = address;
                SetTransportPort(port);

                IPAddress resolvedAddress = null;
                IPEndPoint punchTarget = null;
                if (_natService != null)
                {
                    ConfigureNatPunchPort((ushort)ResolveTransportPort());
                    if (TryResolveIp(address, out resolvedAddress))
                    {
                        if (ShouldSkipPunch(resolvedAddress))
                        {
                            LogInfo($"[YargNetworkManager] Skipping UDP punch for local/private address {resolvedAddress}");
                        }
                        else
                        {
                            punchTarget = new IPEndPoint(resolvedAddress, port);
                        }
                    }
                    else
                    {
                        LogWarning($"[YargNetworkManager] Unable to resolve {address} for UDP punching");
                    }
                }

                _lastJoinAddress = address;
                _lastJoinPort = port;
                _lastJoinPassword = password ?? string.Empty;
                _lastJoinDisplayName = address;

                LogInfo($"[YargNetworkManager] JoinLobby: Attempting to connect to {address}:{port}");
                LogInfo($"[YargNetworkManager] JoinLobby: NetworkClient.active before StartClient: {NetworkClient.active}");
                LogInfo($"[YargNetworkManager] JoinLobby: NetworkServer.active: {NetworkServer.active}");

                _currentLobby = new LobbyInfo
                {
                    lobbyId = "client-joining",
                    lobbyName = "Connecting...",
                    hostName = "Unknown",
                    currentPlayers = 0,
                    maxPlayers = maxPlayers,
                    ipAddress = address,
                    isActive = true,
                    port = port,
                    publicPort = port,
                    publicAddress = address,
                    punchPort = _natService != null ? _natService.PunchPort : NetworkTransportDefaults.DefaultUdpPort,
                    natType = NetworkNatType.Unknown,
                    supportsNatTraversal = false,
                    transportId = Transport.active != null ? Transport.active.GetType().Name : "Unknown",
                    stunServer = string.Empty
                };

                if (!string.IsNullOrEmpty(password))
                {
                    lobbyPassword = password;
                }

                LogInfo($"[YargNetworkManager] Attempting to join lobby at {address}:{port}");
                LogInfo($"[YargNetworkManager] networkAddress is now set to: {networkAddress}");

                StartClient();
                startClientCalled = true;

                LogInfo("[YargNetworkManager] JoinLobby: StartClient() called");
                LogInfo($"[YargNetworkManager] JoinLobby: NetworkClient.active after StartClient: {NetworkClient.active}");

                if (punchTarget != null)
                {
                    _natService?.BeginHolePunch(punchTarget, "client-connect", TimeSpan.FromSeconds(12));
                    punchStarted = true;
                }

                if (punchStarted)
                {
                    try
                    {
                        var joinCompleted = UniTask.WaitUntil(() => !_clientJoinPending, cancellationToken: destroyToken);
                        var punchTimeout = TimeSpan.FromMilliseconds(Math.Max(KcpLowLatencyTimeoutMs, 5000));
                        var timeout = UniTask.Delay(punchTimeout, cancellationToken: destroyToken);
                        await UniTask.WhenAny(joinCompleted, timeout);
                    }
                    catch (OperationCanceledException)
                    {
                        // join attempt aborted, fall through to stop punching
                    }
                    finally
                    {
                        _natService?.StopHolePunch();
                        punchStarted = false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Swallow cancellation (object destroyed)
            }
            catch (Exception ex)
            {
                LogError($"[YargNetworkManager] JoinLobby encountered an unexpected error: {ex}");
            }
            finally
            {
                if (!startClientCalled)
                {
                    _clientJoinPending = false;
                    if (punchStarted)
                    {
                        _natService?.StopHolePunch();
                    }
                }
            }
        }

        public void BeginManualPunch(string endpoint)
        {
            if (_natService == null)
            {
                LogWarning("[YargNetworkManager] Cannot punch without NatTraversalService");
                return;
            }

            ParseEndpoint(endpoint, out var address, out var port);
            if (!TryResolveIp(address, out var remoteAddress))
            {
                LogWarning($"[YargNetworkManager] Unable to resolve {address} for manual punching");
                return;
            }

            ConfigureNatPunchPort((ushort)ResolveTransportPort());
            if (ShouldSkipPunch(remoteAddress))
            {
                LogInfo($"[YargNetworkManager] Skipping manual punch for local/private address {remoteAddress}");
                return;
            }

            _natService.BeginHolePunch(new IPEndPoint(remoteAddress, port), "manual");
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

            _lastJoinDisplayName = lobby.lobbyName;
            int targetPort = lobby.port != 0 ? lobby.port : ResolveTransportPort();
            string endpoint = string.Concat(lobby.ipAddress, ":", targetPort);
            JoinLobby(endpoint, password);
            _currentLobby = lobby;
        }

        /// <summary>
        /// Leave the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            LogInfo($"[YargNetworkManager] LeaveLobby called. _isHost: {_isHost}, _multiplayerShowPlaylist null: {_multiplayerShowPlaylist == null}");
            
            if (_isHost)
            {
                // Cleanup MultiplayerShowPlaylist if it exists
                if (_multiplayerShowPlaylist != null)
                {
                    LogInfo($"[YargNetworkManager] Destroying MultiplayerShowPlaylist with {_multiplayerShowPlaylist.ShowPlaylist.Count} songs");
                    
                    if (NetworkServer.active)
                    {
                        NetworkServer.Destroy(_multiplayerShowPlaylist.gameObject);
                    }
                    else
                    {
                        Destroy(_multiplayerShowPlaylist.gameObject);
                    }
                    _multiplayerShowPlaylist = null;
                    LogInfo("[YargNetworkManager] MultiplayerShowPlaylist destroyed and reference cleared");
                }
                
                StopHost();
                _isHost = false;

                TeardownPortMappingsAsync().Forget();

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

            if (_natService != null)
            {
                _natService.StopKeepAlive();
                _natService.StopHolePunch();
            }

            ResetJoinTracking();
            _clientJoinPending = false;

            OnLobbyLeft?.Invoke();
            LogInfo("Left lobby");
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

            LogInfo($"Lobby updated: {lobbyName}");
        }

        /// <summary>
        /// Get the lobby's connection info for direct connect.
        /// </summary>
        public string GetLobbyConnectionInfo()
        {
            if (!_isHost) return null;

            var lobby = _currentLobby;
            string address = !string.IsNullOrWhiteSpace(lobby?.publicAddress)
                ? lobby.publicAddress
                : networkAddress;

            int port = lobby != null && lobby.publicPort > 0
                ? lobby.publicPort
                : ResolveTransportPort();

            return $"{address}:{port}";
        }

        public int ResolveClientPort()
        {
            return ResolveTransportPort();
        }

        public string GetShareableDirectConnectEndpoint()
        {
            if (_lastPublicEndpoint != null && _lastPublicEndpoint.Port > 0)
            {
                return $"{_lastPublicEndpoint.Address}:{_lastPublicEndpoint.Port}";
            }

            return $"{networkAddress}:{ResolveTransportPort()}";
        }

        private int ResolveTransportPort()
        {
            if (Transport.active is PortTransport portTransport)
            {
                if (portTransport.Port != 0)
                {
                    return portTransport.Port;
                }

                if (Transport.active is KcpTransport)
                {
                    return NetworkTransportDefaults.DefaultUdpPort;
                }

                return NetworkTransportDefaults.DefaultTcpPort;
            }

            if (TryGetComponent<KcpTransport>(out var kcp))
            {
                return kcp.Port != 0 ? kcp.Port : NetworkTransportDefaults.DefaultUdpPort;
            }

            if (TryGetComponent<TelepathyTransport>(out var telepathy))
            {
                return telepathy.port != 0 ? telepathy.port : NetworkTransportDefaults.DefaultTcpPort;
            }

            return NetworkTransportDefaults.DefaultUdpPort;
        }

        private void SetTransportPort(int port)
        {
            port = Mathf.Clamp(port, 0, ushort.MaxValue);

            if (Transport.active is PortTransport portTransport)
            {
                portTransport.Port = (ushort)port;
                ConfigureNatPunchPort((ushort)port);
                return;
            }

            if (TryGetComponent<KcpTransport>(out var kcp))
            {
                kcp.Port = (ushort)port;
                ConfigureNatPunchPort((ushort)port);
                return;
            }

            if (TryGetComponent<TelepathyTransport>(out var telepathy))
            {
                telepathy.port = (ushort)port;
            }
        }

        private void ConfigureNatPunchPort(ushort port)
        {
            if (_natService == null)
            {
                return;
            }

            if (NetworkServer.active)
            {
                return;
            }

            _natService.ConfigurePunchPort(port);
        }

        private async UniTaskVoid ScheduleNatProbe(LobbyInfo lobby)
        {
            if (_natService == null || lobby == null)
            {
                return;
            }

            try
            {
                var token = this.GetCancellationTokenOnDestroy();
                var result = await _natService.ProbeAsync(false, token);

                if (result != null && lobby == _currentLobby)
                {
                    HandlePublicEndpointChanged(result);

                    if (result.HasPublicAddress)
                    {
                        lobby.publicAddress = result.PublicEndPoint.Address.ToString();
                        if (result.PublicEndPoint.Port != 0)
                        {
                            lobby.publicPort = result.PublicEndPoint.Port;
                        }

                        lobby.supportsNatTraversal = true;
                    }

                    lobby.natType = result.NatType;
                    lobby.stunServer = result.StunServer;
                    lobby.punchPort = _natService.PunchPort;

                    var publicEndpoint = result.PublicEndPoint != null ? result.PublicEndPoint.ToString() : "Unavailable";
                    LogInfo($"[YargNetworkManager] NAT probe succeeded via {result.StunServer} - NAT Type: {result.NatType}, Public Endpoint: {publicEndpoint}");

                    var discovery = GetComponent<YargNetworkDiscovery>();
                    discovery?.AdvertiseServer(lobby);

                    // Notify listeners (e.g., lobby UI) that the lobby metadata changed
                    TriggerLobbyJoinedEvent(lobby);
                }

                _natService.BeginKeepAlive();
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] NAT probe failed: {ex}");
            }
        }

        private void HandlePublicEndpointChanged(NatTraversalResult result)
        {
            if (result == null)
            {
                return;
            }

            if (result.PublicEndPoint == null || result.PublicEndPoint.Address.Equals(IPAddress.None))
            {
                _lastPublicEndpoint = null;
                return;
            }

            if (_isHost && !result.IsTransportSocketResult)
            {
                LogWarning("[YargNetworkManager] Ignoring NAT result that was not derived from the transport socket; WAN port may be inaccurate.");
                return;
            }

            bool changed = _lastPublicEndpoint == null || !_lastPublicEndpoint.Equals(result.PublicEndPoint);
            _lastPublicEndpoint = result.PublicEndPoint;

            if (!_isHost || _currentLobby == null)
            {
                return;
            }

            _currentLobby.publicAddress = result.PublicEndPoint.Address.ToString();
            if (result.PublicEndPoint.Port != 0)
            {
                _currentLobby.publicPort = result.PublicEndPoint.Port;
            }

            _currentLobby.supportsNatTraversal = result.HasPublicAddress;
            _currentLobby.punchPort = _natService != null ? _natService.PunchPort : _currentLobby.punchPort;
            _currentLobby.natType = result.NatType;
            _currentLobby.stunServer = result.StunServer ?? string.Empty;

            if (changed && result.HasPublicAddress)
            {
                LogInfo($"[YargNetworkManager] Direct connect (WAN) endpoint updated: {_currentLobby.publicAddress}:{_currentLobby.publicPort}");
            }
        }

        private void ResetJoinTracking()
        {
            _lastJoinAddress = null;
            _lastJoinPort = 0;
            _lastJoinPassword = null;
            _lastJoinDisplayName = null;
        }

        private void HandleNatPunchPacket(IPEndPoint remoteEndPoint, byte[] _)
        {
            if (remoteEndPoint == null)
            {
                return;
            }

            if (ShouldSkipPunch(remoteEndPoint.Address))
            {
                return;
            }

            var now = DateTime.UtcNow;
            string key = remoteEndPoint.ToString();

            if (_recentNatPunches.TryGetValue(key, out var lastSeen) && (now - lastSeen) < NatPunchCacheDuration)
            {
                return;
            }

            _recentNatPunches[key] = now;

            if (_recentNatPunches.Count > 128)
            {
                _recentNatPunches.Clear();
                _recentNatPunches[key] = now;
            }

            LogInfo($"[YargNetworkManager] NAT punch handshake observed from {remoteEndPoint}");

            if (NetworkServer.active && _natService != null)
            {
                _natService.BeginHolePunch(remoteEndPoint, "server-relay", TimeSpan.FromSeconds(12));
            }
        }

        private static bool ShouldResolveLocalAddress(string address)
        {
            if (string.IsNullOrWhiteSpace(address) || address.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (IPAddress.TryParse(address, out var ip))
            {
                return IPAddress.IsLoopback(ip);
            }

            return false;
        }

        private static string TryGetLocalLanAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up)
                    {
                        continue;
                    }

                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback || nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                    {
                        continue;
                    }

                    var properties = nic.GetIPProperties();
                    foreach (var unicast in properties.UnicastAddresses)
                    {
                        if (unicast.Address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        if (IPAddress.IsLoopback(unicast.Address))
                        {
                            continue;
                        }

                        return unicast.Address.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Failed to enumerate network interfaces for LAN address: {ex.Message}");
            }

            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Fallback DNS lookup for LAN address failed: {ex.Message}");
            }

            return string.Empty;
        }

        private void TrySetupPortMapping(int transportPort)
        {
            if (!enableAutomaticPortMapping)
            {
                return;
            }

            if (transportPort <= 0)
            {
                LogWarning("[YargNetworkManager] Skipping automatic port mapping because the transport port is not valid.");
                return;
            }

            if (_upnpMapper == null)
            {
                _upnpMapper = new UpnpPortMapper();
            }

            _portMappingCts?.Cancel();
            _portMappingCts?.Dispose();

            var destroyToken = this.GetCancellationTokenOnDestroy();
            _portMappingCts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            bool mapTcp = Transport.active is not KcpTransport;
            SetupPortMappingsAsync(transportPort, mapTcp, _portMappingCts.Token).Forget();
        }

        private async UniTaskVoid SetupPortMappingsAsync(int transportPort, bool includeTcp, CancellationToken token)
        {
            try
            {
                if (includeTcp)
                {
                    _tcpPortMapping = await _upnpMapper.TryAddMappingAsync(transportPort, "TCP", "YARG Host", token);
                    if (_tcpPortMapping != null)
                    {
                        LogInfo($"[YargNetworkManager] UPnP mapped TCP port {_tcpPortMapping.ExternalPort} to {_tcpPortMapping.LocalAddress}.");
                    }
                }

                var udpPort = _natService != null ? _natService.PunchPort : transportPort;
                if (udpPort <= 0)
                {
                    udpPort = transportPort;
                }
                _udpPortMapping = await _upnpMapper.TryAddMappingAsync(udpPort, "UDP", "YARG NAT Punch", token);
                if (_udpPortMapping != null)
                {
                    LogInfo($"[YargNetworkManager] UPnP mapped UDP port {_udpPortMapping.ExternalPort} to {_udpPortMapping.LocalAddress}.");
                }
            }
            catch (OperationCanceledException)
            {
                // Shutdown already in progress, no need to log.
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Automatic port mapping failed: {ex.Message}");
            }
        }

        private async UniTaskVoid TeardownPortMappingsAsync()
        {
            if (_upnpMapper == null)
            {
                return;
            }

            _portMappingCts?.Cancel();
            _portMappingCts?.Dispose();
            _portMappingCts = null;

            var tcpHandle = _tcpPortMapping;
            var udpHandle = _udpPortMapping;
            _tcpPortMapping = null;
            _udpPortMapping = null;

            try
            {
                await _upnpMapper.RemoveMappingAsync(tcpHandle, CancellationToken.None);
                await _upnpMapper.RemoveMappingAsync(udpHandle, CancellationToken.None);
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Failed to remove UPnP mappings: {ex.Message}");
            }
        }

        private bool TryResolveIp(string host, out IPAddress address)
        {
            address = null;

            if (string.IsNullOrWhiteSpace(host))
            {
                return false;
            }

            if (IPAddress.TryParse(host, out var parsed))
            {
                address = parsed;
                return true;
            }

            try
            {
                var resolved = Dns.GetHostAddresses(host);
                address = resolved.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork)
                          ?? resolved.FirstOrDefault();
                return address != null;
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Failed to resolve host '{host}' for UDP punching: {ex.Message}");
                return false;
            }
        }

        private static bool ShouldSkipPunch(IPAddress address)
        {
            if (address == null)
            {
                return true;
            }

            if (address.IsIPv4MappedToIPv6)
            {
                address = address.MapToIPv4();
            }

            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                var octets = address.GetAddressBytes();
                if (octets.Length != 4)
                {
                    return false;
                }

                if (octets[0] == 10)
                {
                    return true;
                }

                if (octets[0] == 172 && octets[1] >= 16 && octets[1] <= 31)
                {
                    return true;
                }

                if (octets[0] == 192 && octets[1] == 168)
                {
                    return true;
                }

                if (octets[0] == 169 && octets[1] == 254)
                {
                    return true;
                }
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.Equals(IPAddress.IPv6Loopback))
                {
                    return true;
                }
            }

            return false;
        }

        private void ParseEndpoint(string endpoint, out string address, out int port)
        {
            address = endpoint;
            port = ResolveTransportPort();

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            endpoint = endpoint.Trim();

            if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host))
            {
                address = uri.Host;
                if (uri.Port > 0)
                {
                    port = uri.Port;
                }
                return;
            }

            if (endpoint.StartsWith("[", StringComparison.Ordinal))
            {
                int closing = endpoint.IndexOf(']');
                if (closing > 0)
                {
                    address = endpoint.Substring(1, closing - 1);
                    if (closing + 1 < endpoint.Length && endpoint[closing + 1] == ':')
                    {
                        if (int.TryParse(endpoint.Substring(closing + 2), out var parsedPort))
                        {
                            port = parsedPort;
                        }
                    }
                }
                return;
            }

            int colon = endpoint.LastIndexOf(':');
            if (colon > -1 && endpoint.IndexOf(':') == colon)
            {
                string hostPart = endpoint[..colon];
                if (!string.IsNullOrWhiteSpace(hostPart))
                {
                    address = hostPart;
                }

                if (int.TryParse(endpoint[(colon + 1)..], out var parsedPort))
                {
                    port = parsedPort;
                }
            }
        }

        #region Mirror Callbacks

        public override void OnStartServer()
        {
            base.OnStartServer();
            LogInfo($"[YargNetworkManager] OnStartServer called. _multiplayerShowPlaylist is null: {_multiplayerShowPlaylist == null}");
            
            // Check if MultiplayerShowPlaylist already exists in the scene (DontDestroyOnLoad persistence)
            if (_multiplayerShowPlaylist == null)
            {
                // Try to find existing playlist object that may have persisted
                _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
                
                if (_multiplayerShowPlaylist != null)
                {
                    LogInfo($"[YargNetworkManager] Found existing MultiplayerShowPlaylist from previous session with {_multiplayerShowPlaylist.ShowPlaylist.Count} songs - CLEARING");
                    _multiplayerShowPlaylist.ShowPlaylist.Clear();
                    // Don't need to call CmdClearShowPlaylist since server hasn't started yet
                }
            }
            
            // Spawn MultiplayerShowPlaylist as a networked object
            if (_multiplayerShowPlaylist == null)
            {
                LogInfo("[YargNetworkManager] Creating NEW MultiplayerShowPlaylist");
                GameObject playlistGO = new GameObject("MultiplayerShowPlaylist");
                _multiplayerShowPlaylist = playlistGO.AddComponent<YARG.Multiplayer.MultiplayerShowPlaylist>();
                
                // Add NetworkIdentity
                NetworkIdentity netId = playlistGO.AddComponent<NetworkIdentity>();
                
                DontDestroyOnLoad(playlistGO);
                
                // Spawn it on the network with custom hash - clients will use spawn handler
                NetworkServer.Spawn(playlistGO, PLAYLIST_ASSET_HASH);
                
                LogInfo($"[YargNetworkManager] MultiplayerShowPlaylist spawned with netId: {netId.netId}, assetHash: {PLAYLIST_ASSET_HASH:X}");
            }
            else
            {
                // Playlist exists and is already cleared - just re-spawn it on the network
                LogInfo($"[YargNetworkManager] Re-using existing MultiplayerShowPlaylist (already cleared)");
                if (!_multiplayerShowPlaylist.netIdentity.isServer)
                {
                    NetworkServer.Spawn(_multiplayerShowPlaylist.gameObject, PLAYLIST_ASSET_HASH);
                }
            }
            
            LogInfo($"[YargNetworkManager] Server started and ready. Playlist count: {_multiplayerShowPlaylist.ShowPlaylist.Count}");
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            LogInfo("[YargNetworkManager] Host started (Server + Client)");
            
            // For host, the player will be spawned via OnServerAddPlayer automatically
            // when NetworkClient.AddPlayer() is called by Mirror's host logic
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            LogInfo($"[YargNetworkManager] Client started. Connecting to: {networkAddress}");
            
            // Ensure our custom spawn handlers are re-registered after any previous shutdown.
            RegisterMultiplayerShowPlaylistSpawnHandler();

            if (Transport.active is PortTransport portTransport)
            {
                LogInfo($"[YargNetworkManager] Transport port: {portTransport.Port}");
            }
            else if (TryGetComponent<KcpTransport>(out var kcp))
            {
                LogInfo($"[YargNetworkManager] Transport port: {kcp.Port}");
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
            
            LogInfo("[YargNetworkManager] Searching for MultiplayerShowPlaylist...");
            _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
            
            if (_multiplayerShowPlaylist != null)
            {
                LogInfo($"[YargNetworkManager] Found MultiplayerShowPlaylist!");
            }
            else
            {
                LogWarning("[YargNetworkManager] MultiplayerShowPlaylist not found yet");
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
            LogInfo($"[YargNetworkManager] Server: Client connected! ID={conn.connectionId}, Type={connectionType}, Total connections={NetworkServer.connections.Count}");

            OnClientConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Skip all custom cleanup during application shutdown to avoid triggering menu/UI events
            if (_isQuitting)
            {
                LogInfo($"[YargNetworkManager] Application is quitting, skipping OnServerDisconnect cleanup for connection {conn.connectionId}");
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
            LogInfo($"Client disconnected: {conn.connectionId}");
        }

        private bool _hasTriggeredJoinEvent = false;

        public override void OnClientConnect()
        {
            base.OnClientConnect();

            LogInfo($"[YargNetworkManager] Successfully connected to host at {networkAddress}!");

            _clientJoinPending = false;

            _natService?.StopHolePunch();

            // Request player spawning (since autoCreatePlayer is disabled)
            if (!NetworkClient.ready)
            {
                NetworkClient.Ready();
                LogInfo("[YargNetworkManager] Client ready state set");
            }
            else
            {
                LogInfo("[YargNetworkManager] Client already ready");
            }

            // Request to add player for this connection (only if we don't have one)
            if (NetworkClient.localPlayer == null)
            {
                NetworkClient.AddPlayer();
                LogInfo("[YargNetworkManager] Requested player spawn");
            }
            else
            {
                LogInfo("[YargNetworkManager] Client already has a local player, skipping AddPlayer()");
            }

            ScheduleLocalPlayerSlotSync();

            // Only trigger OnLobbyJoined event once
            if (!_hasTriggeredJoinEvent)
            {
                _hasTriggeredJoinEvent = true;
                
                // Ensure we have lobby info (should have been set in JoinLobby or CreateLobby)
                if (_currentLobby == null)
                {
                    LogWarning("[YargNetworkManager] Connected but _currentLobby is null. Creating default lobby info.");
                    _currentLobby = new LobbyInfo
                    {
                        lobbyId = "unknown",
                        lobbyName = "Connected Lobby",
                        hostName = "Unknown Host",
                        currentPlayers = 1,
                        maxPlayers = maxPlayers,
                        ipAddress = networkAddress,
                        publicAddress = networkAddress,
                        transportId = Transport.active != null ? Transport.active.GetType().Name : "Unknown",
                        port = ResolveTransportPort(),
                        publicPort = ResolveTransportPort(),
                        punchPort = _natService != null ? _natService.PunchPort : NetworkTransportDefaults.DefaultUdpPort,
                        supportsNatTraversal = false,
                        natType = NetworkNatType.Unknown,
                        stunServer = string.Empty,
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
                
                LogInfo($"[YargNetworkManager] Triggering OnLobbyJoined for: {_currentLobby.lobbyName}");
                OnLobbyJoined?.Invoke(_currentLobby);
            }
        }

        public override void OnClientDisconnect()
        {
            base.OnClientDisconnect();

            // Skip cleanup during application shutdown to prevent menu navigation errors
            if (_isQuitting)
            {
                LogInfo("[YargNetworkManager] Application is quitting, skipping OnClientDisconnect cleanup");
                return;
            }

            _currentLobby = null;
            _connectedPlayers.Clear();
            _hasTriggeredJoinEvent = false;

            _natService?.StopHolePunch();
            _clientJoinPending = false;
            ResetJoinTracking();

            OnLobbyLeft?.Invoke();
            LogInfo("Disconnected from host");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Mirror clears spawn handlers on shutdown; prepare for the next connection immediately.
            RegisterMultiplayerShowPlaylistSpawnHandler();

            _localSlotSyncPending = false;
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            TeardownPortMappingsAsync().Forget();
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TeardownPortMappingsAsync().Forget();
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Check if this connection already has a player
            if (conn.identity != null)
            {
                LogWarning($"[YargNetworkManager] Connection {conn.connectionId} already has a player. Skipping duplicate spawn.");
                return;
            }

            LogInfo($"[YargNetworkManager] Adding player for connection {conn.connectionId}");

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
                    LogWarning($"[YargNetworkManager] Connection {conn.connectionId} not found in _connectedPlayers dictionary");
                }

                OnPlayerJoined?.Invoke(playerData);
                LogInfo($"[YargNetworkManager] Player spawned successfully for connection {conn.connectionId}");
                
                // Show toast notification for player join (except for host joining their own lobby)
                if (conn.connectionId != 0)
                {
                    // Use RPC to show toast to all clients
                    playerData.RpcShowPlayerJoinedToast(_playerName);
                }
                
                // Sync lobby info to the newly joined client (not to host)
                if (_currentLobby != null && conn is not LocalConnectionToClient)
                {
                    LogInfo($"[YargNetworkManager] Syncing lobby info to client {conn.connectionId}");
                    playerData.TargetSyncLobbyInfo(_currentLobby.lobbyName, _currentLobby.hostName, 
                        _currentLobby.maxPlayers, _currentLobby.hasPassword, (int)_currentLobby.privacyMode);
                }
            }
            else
            {
                LogError($"[YargNetworkManager] Player prefab does not have NetworkPlayerData component!");
            }
        }

        public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        {
            base.OnServerError(conn, error, reason);
            LogError($"[YargNetworkManager] Server error on connection {conn.connectionId}: {error} - {reason}");
            OnNetworkError?.Invoke($"Server error: {reason}");
        }

        public override void OnClientError(TransportError error, string reason)
        {
            base.OnClientError(error, reason);
            LogError($"[YargNetworkManager] Client error: {error} - {reason}");
            LogError($"[YargNetworkManager] Was trying to connect to: {networkAddress}");
            _natService?.StopHolePunch();
            _clientJoinPending = false;
            if (!_isHost && NetworkClient.active)
            {
                StopClient();
            }

            ResetJoinTracking();
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
                LogInfo($"[YargNetworkManager] GetAllPlayers ({context}): Found {allPlayers.Count} players (sorted by netId)");
                foreach (var player in allPlayers)
                {
                    if (player != null)
                    {
                        LogInfo($"[YargNetworkManager] - {player.PlayerName} (netId: {player.netId}) in scene: {player.gameObject.scene.name}");
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
                LogWarning("[YargNetworkManager] KickPlayer called but server is not active");
                return;
            }
            
            if (!isNetworkActive || conn == null)
            {
                LogWarning("[YargNetworkManager] Cannot kick player - invalid connection");
                return;
            }
            
            LogInfo($"[YargNetworkManager] Kicking player on connection {conn.connectionId}");
            
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
                LogWarning("[YargNetworkManager] StartSongSelection called but not host");
                return;
            }
            
            LogInfo("[YargNetworkManager] Host starting song selection for all clients");
            
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
            LogInfo($"[YargNetworkManager] Song selected event triggered: {song.Name}");
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
                LogWarning("[YargNetworkManager] SyncSongSelection called but not host");
                return;
            }
            
            LogInfo($"[YargNetworkManager] Syncing song selection to all clients: {song.Name}");
            
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
                LogWarning("[YargNetworkManager] StartMultiplayerSong called but not host");
                return;
            }
            
            LogInfo($"[YargNetworkManager] Starting multiplayer song for all clients: {song.Name}");
            
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
                LogWarning("[YargNetworkManager] StartMultiplayerGameplay called but not host");
                return;
            }
            
            LogInfo("[YargNetworkManager] Starting gameplay for all clients");
            
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
                LogWarning("[YargNetworkManager] RestartMultiplayerGameplay called but not host");
                return;
            }
            
            LogInfo("[YargNetworkManager] Restarting gameplay for all clients");
            
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
                LogWarning("[YargNetworkManager] WaitForMultiplayerGameplayStartAsync called with no local player identity.");
                return;
            }

            var playerData = identity.GetComponent<NetworkPlayerData>();
            if (playerData == null)
            {
                LogWarning("[YargNetworkManager] Local player does not have NetworkPlayerData component.");
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
                LogInfo($"[YargNetworkManager] {playerData.PlayerName} marked gameplay ready.");
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

            LogInfo("[YargNetworkManager] All players reported gameplay ready. Broadcasting coordinated start signal.");
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

            LogWarning("[YargNetworkManager] Force completing gameplay start barrier due to timeout.");
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

            UnityMainThreadCallback.QueueEvent(ForceCompleteGameplayStartBarrierInternal);
        }

        #endregion

        #region Gameplay Failure Tracking

        [Server]
        public void ResetBandFailureTracking()
        {
            _serverFailedPlayers.Clear();
            _serverBandFailureTriggered = false;

            foreach (var player in GetAllPlayers())
            {
                player?.ServerClearFailureFlag();
            }
        }

        [Server]
        internal void ServerOnPlayerFailed(NetworkPlayerData playerData)
        {
            if (playerData == null)
            {
                return;
            }

            if (!_serverFailedPlayers.Add(playerData.netId))
            {
                return;
            }

            EvaluateBandFailureState();
        }

        [Server]
        private void EvaluateBandFailureState()
        {
            if (_serverBandFailureTriggered)
            {
                return;
            }

            var players = GetAllPlayers();
            if (players.Count == 0)
            {
                return;
            }

            bool anyActivePlayers = false;
            foreach (var player in players)
            {
                if (player == null)
                {
                    continue;
                }

                if (!player.GameplayReady)
                {
                    continue;
                }

                anyActivePlayers = true;

                if (!player.HasFailed)
                {
                    return;
                }
            }

            if (!anyActivePlayers)
            {
                return;
            }

            _serverBandFailureTriggered = true;
            LogInfo("[YargNetworkManager] All active players failed. Broadcasting band failure.");

            foreach (var player in players)
            {
                if (player == null)
                {
                    continue;
                }

                player.TargetHandleBandFailed(player.connectionToClient);
            }
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
                LogWarning("[YargNetworkManager] SyncPracticeMode called but not host");
                return;
            }
            
            LogInfo($"[YargNetworkManager] Syncing practice mode to all clients: {isPractice}");
            
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
                LogWarning("[YargNetworkManager] QuitMultiplayerGameplay called but not host");
                return;
            }
            
            LogInfo("[YargNetworkManager] Quitting gameplay for all clients");
            
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
                LogWarning("[YargNetworkManager] AdvanceAfterScoreScreen called but not host");
                return;
            }

            var players = GetAllPlayers();
            foreach (var player in players)
            {
                player?.SetReadyStateServer(false);
            }

            var currentPlaylist = GlobalVariables.State.ShowSongs;
            var completedSong = (GlobalVariables.State.PlayingAShow &&
                                 currentPlaylist != null &&
                                 currentPlaylist.Count > 0 &&
                                 GlobalVariables.State.ShowIndex >= 0 &&
                                 GlobalVariables.State.ShowIndex < currentPlaylist.Count)
                ? currentPlaylist[GlobalVariables.State.ShowIndex]
                : null;

            if (completedSong != null)
            {
                if (_multiplayerShowPlaylist == null)
                {
                    _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
                }

                if (_multiplayerShowPlaylist != null)
                {
                    _multiplayerShowPlaylist.HostRemoveSong(completedSong);
                }
                else if (currentPlaylist != null)
                {
                    GlobalVariables.State.ShowSongs = currentPlaylist
                        .Where((song, index) => index != GlobalVariables.State.ShowIndex)
                        .ToList();
                }
            }

            currentPlaylist = GlobalVariables.State.ShowSongs;
            bool hasNextSong = GlobalVariables.State.PlayingAShow &&
                               currentPlaylist != null &&
                               currentPlaylist.Count > 0;

            if (hasNextSong)
            {
                GlobalVariables.State.ShowIndex = Mathf.Clamp(
                    GlobalVariables.State.ShowIndex,
                    0,
                    currentPlaylist.Count - 1);

                var nextSong = currentPlaylist[GlobalVariables.State.ShowIndex];
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
                GlobalVariables.State.CurrentSong = null;

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
                LogWarning("[YargNetworkManager] SyncMenuNavigation called but not host");
                return;
            }
            
            string action = popMenu ? "PopMenu" : $"PushMenu({targetMenu})";
            LogInfo($"[YargNetworkManager] Syncing menu navigation to all clients: {action}");
            
            // Broadcast via all NetworkPlayerData objects (they're spawned and can send RPCs)
            var allPlayers = GetAllPlayers();
            if (allPlayers.Count > 0)
            {
                // Use first player's NetworkPlayerData to broadcast to all clients
                allPlayers[0].RpcNavigateMenu(popMenu, (int)targetMenu);
            }
            else
            {
                LogWarning("[YargNetworkManager] No NetworkPlayerData objects found to send RPC!");
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

            LogInfo($"[YargNetworkManager] Set menu navigation after scene load: {route}");
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
            public string publicAddress;
            public string transportId;
            public string stunServer;
            public int currentPlayers;
            public int maxPlayers;
            public LobbyPrivacyMode privacyMode;
            public bool hasPassword;
            public string password;
            public bool isActive;
            public int port;
            public int publicPort;
            public int punchPort;
            public bool supportsNatTraversal;
            public NetworkNatType natType;
            public long lastSeen;

            public override string ToString()
            {
                return $"{lobbyName} ({currentPlayers}/{maxPlayers}) - Host: {hostName} @ {ipAddress}:{port}";
            }
        }

        public override void OnApplicationQuit()
        {
            _isQuitting = true;
            LogInfo("[YargNetworkManager] OnApplicationQuit called, setting _isQuitting = true");
            TeardownPortMappingsAsync().Forget();
            base.OnApplicationQuit();
        }

        public override void OnDestroy()
        {
            _isQuitting = true;
            LogInfo("[YargNetworkManager] OnDestroy called, setting _isQuitting = true");
            if (Instance == this)
            {
                PlayerContainer.PlayerAdded -= OnLocalPlayerAddedToContainer;
                PlayerContainer.PlayerRemoved -= OnLocalPlayerRemovedFromContainer;
                Instance = null;
            }
            if (_natService != null)
            {
                _natService.PunchPacketReceived -= HandleNatPunchPacket;
                _natService.PublicEndpointChanged -= HandlePublicEndpointChanged;
            }
            _lastPublicEndpoint = null;
            _natService?.StopKeepAlive();
            TeardownPortMappingsAsync().Forget();
            base.OnDestroy();
        }
    }
}