using Mirror;
using kcp2k;
using UnityEngine;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Cysharp.Threading.Tasks;
using YARG.Networking.Bookmarks;
using YARG.Networking.UPnP;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Multiplayer;
using YARG.Player;
using YARG.Core.Game;
using YARG.Core;
using YARG;
using YARG.Menu.Persistent;
using YARG.Song;

namespace YARG.Networking
{
    [Serializable]
    public struct LobbyProbeRequestMessage : NetworkMessage
    {
        public string clientVersion;
    }

    [Serializable]
    public struct LobbyProbeResponseMessage : NetworkMessage
    {
        public bool success;
        public string lobbyId;
        public string lobbyName;
        public string hostName;
        public string publicAddress;
        public string transportId;
        public int currentPlayers;
        public int maxPlayers;
        public bool hasPassword;
        public int privacyMode;
        public ushort port;
        public ushort publicPort;
        public string[] playerNames;
        public int[] playerInstruments;
    }

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
        private int _hostConnectionId = -1;
        private static readonly object ProbeConnectionToken = new();
        private readonly HashSet<int> _probeConnectionIds = new();
        private LobbyInfo _currentLobby;
        private string _playerName;
        private bool _isHost = false;
        private bool _isDedicatedServer = false;
        private YARG.Multiplayer.MultiplayerShowPlaylist _multiplayerShowPlaylist;
        private static bool _isQuitting = false;
        private string _lastJoinAddress;
        private int _lastJoinPort;
        private string _lastJoinPassword;
        private string _lastJoinDisplayName;
        private UpnpPortMapper _upnpMapper;
        private UpnpPortMappingHandle _tcpPortMapping;
        private UpnpPortMappingHandle _udpPortMapping;
        private CancellationTokenSource _portMappingCts;
        private CancellationTokenSource _publicEndpointResolveCts;
        private bool _clientJoinPending;
        private bool _localSlotSyncPending;
        private int _clientSessionCounter;
        private int _activeClientSessionId;
        private int _activeProbeSessionId;

        // Transient connection used to probe lobby info without joining.
        private bool _probeConnectionPending;
        private bool _probeConnectionActive;
        private bool _probeHasCompleted;
        private UniTaskCompletionSource<LobbyInfo?> _probeCompletionSource;
        private CancellationTokenRegistration _probeCancellationRegistration;
        private string _probePreviousAddress = string.Empty;
        private int _probePreviousPort;
        private const int PROBE_TIMEOUT_MS = 5000;

        private enum ProbeCompletionState
        {
            Success,
            Failed,
            Timeout,
            Canceled
        }

        private readonly Dictionary<uint, HashSet<HashWrapper>> _playerSongLibraries = new();
        private readonly HashSet<uint> _playersPendingSongSync = new();
        private HashSet<HashWrapper>? _sharedSongHashes;
        private bool _sharedSongSyncComplete = true;
        private bool _pendingSongSelectionBroadcast;
        private readonly Dictionary<uint, Stopwatch> _songLibraryReceiveTimers = new();
        private readonly Dictionary<uint, Stopwatch> _songLibraryFirstChunkTimers = new();
        private readonly Dictionary<uint, int> _songLibraryChunkCounts = new();
        private readonly Dictionary<uint, int> _songLibraryReceivedHashes = new();
        private readonly Dictionary<uint, long> _songLibraryReceivedBytes = new();

        private readonly HashSet<uint> _serverGameplayReadyPlayers = new();
        private bool _serverGameplayBarrierActive;
        private double _serverGameplayStartTime;
        private const float GAMEPLAY_START_COUNTDOWN_SECONDS = 0.25f;

        private readonly HashSet<uint> _serverFailedPlayers = new();
        private bool _serverBandFailureTriggered;
        private bool _isTransitioningToHost;
        private bool _clientStopPending;
        private bool _suppressClientDisconnectNotification;
        private readonly HashSet<uint> _clientNotifiedPlayers = new();

        private UniTaskCompletionSource<double> _clientGameplayStartTcs;
        private bool _clientReadyReported;

        public int MaxPlayers => maxPlayers;
        public int MaxLocalPlayersPerClient => maxLocalPlayersPerClient;
        public LobbyInfo CurrentLobby => _currentLobby;
        public string PlayerName => _playerName;
        public bool IsHosting => _isHost;
        public bool IsDedicatedServer => _isDedicatedServer;
        public Dictionary<NetworkConnectionToClient, List<NetworkPlayerData>> ConnectedPlayers => _connectedPlayers;
        public bool IsConnected => isNetworkActive && !_isHost;
        public YARG.Multiplayer.MultiplayerShowPlaylist MultiplayerShowPlaylist => _multiplayerShowPlaylist;
        public int DefaultPort => ResolveTransportPort();
        public int SuggestedDirectConnectPort => ResolveTransportPort();
        public bool IsJoinInProgress => _clientJoinPending;
        public bool IsSharedSongSyncComplete => _sharedSongSyncComplete;
        internal bool IsProbeContextActive => _probeConnectionPending || _probeConnectionActive || _probeCompletionSource != null || _probeHasCompleted;
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
        public event Action<bool> OnSharedSongSyncStateChanged;

        public enum LobbyPrivacyMode
        {
            Public,
            Private
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

            if (GetComponent<YargNetworkDiscovery>() == null)
            {
                gameObject.AddComponent<YargNetworkDiscovery>();
            }

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

            // Set default player name (will be updated from profile when network player spawns)
            _playerName = $"Player_{UnityEngine.Random.Range(1000, 9999)}";

            // Configure Mirror settings
            maxConnections = maxPlayers;
            
            // Disable auto-create player - we'll handle spawning manually
            autoCreatePlayer = false;
            
            // Register spawn handler for MultiplayerShowPlaylist
            RegisterMultiplayerShowPlaylistSpawnHandler();

            EnsurePasswordAuthenticator();

            RegisterProbeHandlers();

            LogInfo("[YargNetworkManager] Initialized with autoCreatePlayer disabled");

            PlayerContainer.PlayerAdded += OnLocalPlayerAddedToContainer;
            PlayerContainer.PlayerRemoved += OnLocalPlayerRemovedFromContainer;
        }

        public NetworkPlayerData GetLocalPrimaryPlayerData()
        {
            if (NetworkClient.active)
            {
                var identity = NetworkClient.localPlayer;
                if (identity != null && identity.TryGetComponent(out NetworkPlayerData localData))
                {
                    return localData;
                }

                foreach (var spawned in NetworkClient.spawned.Values)
                {
                    if (spawned != null && spawned.TryGetComponent(out NetworkPlayerData candidate) && candidate.IsLocalUser)
                    {
                        return candidate;
                    }
                }
            }

            if (NetworkServer.active && NetworkServer.localConnection != null)
            {
                if (_connectedPlayers.TryGetValue(NetworkServer.localConnection, out var players) && players != null)
                {
                    foreach (var player in players)
                    {
                        if (player != null)
                        {
                            return player;
                        }
                    }
                }
            }

            return null;
        }

        public NetworkPlayerData GetCurrentHostPlayer()
        {
            if (NetworkServer.active)
            {
                foreach (var kvp in _connectedPlayers)
                {
                    if (kvp.Value == null)
                    {
                        continue;
                    }

                    foreach (var player in kvp.Value)
                    {
                        if (player != null && player.IsHost)
                        {
                            return player;
                        }
                    }
                }
            }

            if (NetworkClient.active)
            {
                foreach (var identity in NetworkClient.spawned.Values)
                {
                    if (identity != null && identity.TryGetComponent(out NetworkPlayerData data) && data.IsHost)
                    {
                        return data;
                    }
                }
            }

            return null;
        }

        public bool LocalUserIsHost()
        {
            if (_isHost && NetworkServer.active && !NetworkClient.active)
            {
                return true;
            }

            var localPlayer = GetLocalPrimaryPlayerData();
            return localPlayer != null && localPlayer.IsHost;
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

            transport.DualMode = false;
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

            RefreshHostOwnership();
        }

        /// <summary>
        /// Create a new lobby and start hosting.
        /// </summary>
        public LobbyInfo CreateLobby(string lobbyName, int maxPlayers, LobbyPrivacyMode privacyMode, string password = "")
        {
            CancelActiveProbe("Starting local host session");
            _hasTriggeredJoinEvent = false;
            StopActiveClient("Preparing to host");

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

            int transportPort = ResolveTransportPort();
            if (transportPort <= 0)
            {
                transportPort = NetworkTransportDefaults.DefaultUdpPort;
            }

            // Ensure the active transport and the serialized component stay in sync.
            SetTransportPort(transportPort);
            if (transport is KcpTransport kcp)
            {
                kcp.Port = (ushort)transportPort;
            }

            string connectionInfo = $"{networkAddress}:{transportPort}";

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
                port = transportPort,
                publicPort = transportPort,
                publicAddress = networkAddress,
                transportId = Transport.active != null ? Transport.active.GetType().Name : "Unknown"
            };
            LogInfo($"[YargNetworkManager] Creating lobby '{lobbyName}' on {connectionInfo}");

            // Start hosting (guard against double-start which Mirror logs as "Server or Client already started")
            if (!NetworkServer.active)
            {
                _isTransitioningToHost = true;
                try
                {
                    if (_isDedicatedServer)
                    {
                        StartServer();
                    }
                    else
                    {
                        StartHost();
                    }
                    _isHost = true;
                }
                catch
                {
                    _isTransitioningToHost = false;
                    throw;
                }
                finally
                {
                    if (NetworkServer.active)
                    {
                        _isTransitioningToHost = false;
                    }
                }
            }
            else
            {
                // Server already active - assume we're hosting or a previous host wasn't cleaned up.
                LogWarning("[YargNetworkManager] StartHost skipped: NetworkServer already active.");
                _isHost = true;
            }

            // Log sanitized server state for diagnostics (do not log actual password value)
            LogInfo($"[YargNetworkManager] CreateLobby: hasPassword={_currentLobby.hasPassword}, transport={(Transport.active!=null?Transport.active.GetType().Name:"None")}, port={transportPort}");

            // Start broadcasting lobby for discovery
            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.AdvertiseServer(_currentLobby);
            }

            LogInfo($"[YargNetworkManager] Lobby created successfully! Connection info: {connectionInfo}");
            LogInfo($"[YargNetworkManager] CreateLobby: NetworkServer.active after StartHost: {NetworkServer.active}");
            LogInfo($"[YargNetworkManager] CreateLobby: NetworkServer listening on port: {transportPort}");
            
            // Trigger OnLobbyCreated event (but don't navigate yet)
            OnLobbyCreated?.Invoke(_currentLobby);
            
            // Host will also trigger OnClientConnect which will fire OnLobbyJoined for navigation

            TrySetupPortMapping(transportPort);
            BeginPublicEndpointResolution(transportPort);

            return _currentLobby;
        }

        internal void LaunchDedicatedServer(DedicatedServerConfig config)
        {
            if (!config.Enabled)
            {
                return;
            }

            if (isNetworkActive || NetworkServer.active)
            {
                LogWarning("[YargNetworkManager] Dedicated server launch requested while network is already active.");
                return;
            }

            _isDedicatedServer = true;
            lobbyName = config.LobbyName;
            privacyMode = config.PrivacyMode;
            lobbyPassword = config.PrivacyMode == LobbyPrivacyMode.Private ? config.Password : string.Empty;
            SetPlayerName(config.HostName);

            CreateLobby(lobbyName, config.MaxPlayers, privacyMode, lobbyPassword);
            LogInfo($"[YargNetworkManager] Dedicated server initialized. Lobby='{lobbyName}', maxPlayers={this.maxPlayers}, privacy={privacyMode}");
        }

        internal void ConfigureDedicatedServerNetworking(int? transportPortOverride, int? discoveryPortOverride, bool advertiseDiscovery)
        {
            if (transportPortOverride.HasValue && transportPortOverride.Value > 0)
            {
                SetTransportPort(transportPortOverride.Value);
            }

            var discovery = GetComponent<YargNetworkDiscovery>();
            if (discovery != null)
            {
                discovery.ConfigureDiscoveryOptions(advertiseDiscovery, discoveryPortOverride);
            }
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

                CancelActiveProbe("Joining lobby");

                ParseEndpoint(endpoint, out var address, out var port);
                if (string.IsNullOrWhiteSpace(address))
                {
                    LogError("[YargNetworkManager] JoinLobby failed: endpoint missing address");
                    return;
                }

                networkAddress = address;
                SetTransportPort(port);

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
                    transportId = Transport.active != null ? Transport.active.GetType().Name : "Unknown"
                };

                if (!string.IsNullOrEmpty(password))
                {
                    lobbyPassword = password;
                }

                LogInfo($"[YargNetworkManager] Attempting to join lobby at {address}:{port}");
                LogInfo($"[YargNetworkManager] networkAddress is now set to: {networkAddress}");

                StartClientAndTrack();
                startClientCalled = true;

                LogInfo("[YargNetworkManager] JoinLobby: StartClient() called");
                LogInfo($"[YargNetworkManager] JoinLobby: NetworkClient.active after StartClient: {NetworkClient.active}");

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
                }
            }
        }

        /// <summary>
        /// Join a discovered lobby.
        /// </summary>
        public void JoinDiscoveredLobby(LobbyInfo lobby, string password = "")
        {
            // NOTE: discovery responses do not include server-side passwords.
            // If we have a locally-stored password for this lobby (e.g. from a bookmark),
            // validate it here and reject obvious mismatches. If the lobby is only
            // discovered (live) and no local password is known, allow the join to
            // proceed if the caller supplied a password (UI should prompt when needed).
            if (lobby.hasPassword && !string.IsNullOrEmpty(lobby.password) && lobby.password != password)
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
                
                CancelPublicEndpointResolution();
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
            _hostConnectionId = -1;
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

        public async UniTask<LobbyInfo?> ProbeLobbyAsync(string address, int port, int timeoutMilliseconds = PROBE_TIMEOUT_MS, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                return null;
            }

            if (_probeConnectionPending || _probeConnectionActive || _probeCompletionSource != null)
            {
                LogWarning("[YargNetworkManager] Probe request ignored because another probe is already in progress.");
                return null;
            }

            if (_clientJoinPending || NetworkClient.isConnected || NetworkClient.active)
            {
                LogWarning("[YargNetworkManager] Probe request ignored because the client is already connected or joining a lobby.");
                return null;
            }

            if (_isHost && NetworkServer.active)
            {
                // Hosts already have local lobby data; remote discovery is unnecessary.
                return _currentLobby;
            }

            var destroyToken = this.GetCancellationTokenOnDestroy();
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, destroyToken);
            CancellationToken linkedToken = linkedCts.Token;

            _probeCompletionSource = new UniTaskCompletionSource<LobbyInfo?>();
            _probeHasCompleted = false;
            _probeConnectionPending = true;
            _probePreviousAddress = networkAddress;
            _probePreviousPort = ResolveTransportPort();
            int probeSessionId = 0;

            try
            {
                networkAddress = address.Trim();
                SetTransportPort(Mathf.Clamp(port, 1, ushort.MaxValue));

                probeSessionId = StartClientAndTrack();
                _activeProbeSessionId = probeSessionId;

                _probeCancellationRegistration = linkedToken.Register(() =>
                {
                    UniTask.Post(() =>
                    {
                        if (_probeCompletionSource != null)
                        {
                            CompleteProbe(null, ProbeCompletionState.Canceled);
                        }

                        if (_activeClientSessionId == probeSessionId && NetworkClient.isConnected)
                        {
                            NetworkClient.Disconnect();
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Failed to start probe connection to {address}:{port}: {ex.Message}");
                try
                {
                    _probeCancellationRegistration.Dispose();
                }
                catch { }

                _probeCompletionSource = null;
                _probeConnectionPending = false;
                networkAddress = _probePreviousAddress;
                SetTransportPort(_probePreviousPort);
                if (probeSessionId != 0 && _activeClientSessionId == probeSessionId)
                {
                    _activeClientSessionId = 0;
                }
                _activeProbeSessionId = 0;
                return null;
            }

            var timeoutUniTask = UniTask.Delay(Mathf.Max(1000, timeoutMilliseconds), cancellationToken: linkedToken);
            var resultTask = _probeCompletionSource.Task;

            var timeoutTask = timeoutUniTask.AsTask();
            var probeTask = resultTask.AsTask();

            Task completedTask;
            try
            {
                completedTask = await Task.WhenAny(probeTask, timeoutTask);
            }
            catch (OperationCanceledException)
            {
                throw;
            }

            if (completedTask == timeoutTask && _probeCompletionSource != null)
            {
                CompleteProbe(null, ProbeCompletionState.Timeout);
                if (_activeClientSessionId == probeSessionId && NetworkClient.isConnected)
                {
                    NetworkClient.Disconnect();
                }
            }

            LobbyInfo? result;
            try
            {
                result = await resultTask;
            }
            finally
            {
                // Wait until the probe connection has fully torn down to avoid stomping real connection state.
                await UniTask.WaitUntil(() => !_probeConnectionPending && !_probeConnectionActive);

                if (probeSessionId != 0)
                {
                    StopActiveClient("Probe completed", probeSessionId);
                }
            }

            return result;
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
            return $"{networkAddress}:{ResolveTransportPort()}";
        }

        /// <summary>
        /// Return the password that was provided for the pending join (if any).
        /// Used by the client-side authenticator to send the password during authentication.
        /// </summary>
        public string GetPendingJoinPassword()
        {
            return _lastJoinPassword ?? string.Empty;
        }

        /// <summary>
        /// Return the server's configured lobby password (from the inspector or runtime).
        /// This is a safe, read-only accessor used by the authenticator as a fallback
        /// when authoritative LobbyInfo isn't available at authentication time.
        /// </summary>
        public string GetServerLobbyPassword()
        {
            return lobbyPassword ?? string.Empty;
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
                return;
            }

            if (TryGetComponent<KcpTransport>(out var kcp))
            {
                kcp.Port = (ushort)port;
                return;
            }

            if (TryGetComponent<TelepathyTransport>(out var telepathy))
            {
                telepathy.port = (ushort)port;
            }
        }

        private void ResetJoinTracking()
        {
            _lastJoinAddress = null;
            _lastJoinPort = 0;
            _lastJoinPassword = null;
            _lastJoinDisplayName = null;
        }

        private void EnsurePasswordAuthenticator()
        {
            if (authenticator != null)
            {
                return;
            }

            var passwordAuthenticator = GetComponent<PasswordAuthenticator>();
            if (passwordAuthenticator == null)
            {
                passwordAuthenticator = gameObject.AddComponent<PasswordAuthenticator>();
                if (Application.isPlaying)
                {
                    LogInfo("[YargNetworkManager] Added PasswordAuthenticator to enforce lobby passwords.");
                }
            }
            else if (Application.isPlaying)
            {
                LogInfo("[YargNetworkManager] Using existing PasswordAuthenticator component.");
            }

            authenticator = passwordAuthenticator;
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
                var candidates = new List<(IPAddress address, bool hasGateway, int preference)>(8);

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

                    if (IsInterfaceExcluded(nic))
                    {
                        continue;
                    }

                    var properties = nic.GetIPProperties();
                    bool hasGateway = properties.GatewayAddresses.Any(g => IsRoutableGateway(g?.Address));

                    foreach (var unicast in properties.UnicastAddresses)
                    {
                        var address = unicast.Address;
                        if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
                        {
                            continue;
                        }

                        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || IsLinkLocal(address))
                        {
                            continue;
                        }

                        int preference = GetIpv4PreferenceScore(address);
                        candidates.Add((address, hasGateway, preference));
                    }
                }

                if (candidates.Count > 0)
                {
                    var selected = candidates
                        .OrderByDescending(c => GetCompositePreference(c.preference, c.hasGateway))
                        .ThenByDescending(c => c.preference)
                        .ThenByDescending(c => c.hasGateway)
                        .First();

                    return selected.address.ToString();
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
                    if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip) && !IsLinkLocal(ip))
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

        internal void ClientRegisterPlayer(NetworkPlayerData playerData)
        {
            if (playerData == null || NetworkServer.active || !NetworkClient.active)
            {
                return;
            }

            if (_clientNotifiedPlayers.Add(playerData.netId))
            {
                OnPlayerJoined?.Invoke(playerData);
            }
        }

        internal void ClientUnregisterPlayer(NetworkPlayerData playerData)
        {
            if (playerData == null || NetworkServer.active)
            {
                return;
            }

            if (_clientNotifiedPlayers.Remove(playerData.netId))
            {
                OnPlayerLeft?.Invoke(playerData);
            }
        }

        private static bool IsInterfaceExcluded(NetworkInterface nic)
        {
            string description = nic.Description ?? string.Empty;
            string name = nic.Name ?? string.Empty;

            if (description.Contains("Tailscale", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("ZeroTier", StringComparison.OrdinalIgnoreCase) ||
                description.Contains("Hamachi", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Tailscale", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsRoutableGateway(IPAddress? address)
        {
            if (address == null)
            {
                return false;
            }

            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            if (address.Equals(IPAddress.Any) || IPAddress.IsLoopback(address))
            {
                return false;
            }

            return true;
        }

        private static bool IsLinkLocal(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return false;
            }

            var bytes = address.GetAddressBytes();
            return bytes[0] == 169 && bytes[1] == 254;
        }

        private static int GetIpv4PreferenceScore(IPAddress address)
        {
            if (address == null || address.AddressFamily != AddressFamily.InterNetwork)
            {
                return 0;
            }

            var bytes = address.GetAddressBytes();

            if (bytes[0] == 192 && bytes[1] == 168)
            {
                return 4;
            }

            if (bytes[0] == 10)
            {
                return 3;
            }

            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
            {
                return 2;
            }

            if (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
            {
                return 1;
            }

            return 0;
        }

        private static int GetCompositePreference(int basePreference, bool hasGateway)
        {
            int gatewayBonus = hasGateway ? 1 : 0;
            return (basePreference * 2) + gatewayBonus;
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

        private void BeginPublicEndpointResolution(int transportPort)
        {
            CancelPublicEndpointResolution();

            if (!_isHost || _currentLobby == null)
            {
                return;
            }

            if (transportPort <= 0)
            {
                return;
            }

            var destroyToken = this.GetCancellationTokenOnDestroy();
            var cts = CancellationTokenSource.CreateLinkedTokenSource(destroyToken);
            _publicEndpointResolveCts = cts;
            string lobbyId = _currentLobby.lobbyId ?? string.Empty;
            ResolvePublicEndpointAsync(transportPort, lobbyId, cts).Forget();
        }

        private void CancelPublicEndpointResolution()
        {
            var cts = _publicEndpointResolveCts;
            if (cts == null)
            {
                return;
            }

            _publicEndpointResolveCts = null;

            try
            {
                cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already disposed by resolver.
            }
            catch (AggregateException)
            {
                // Ignore cancellation aggregate during shutdown.
            }
        }

        private async UniTaskVoid ResolvePublicEndpointAsync(int transportPort, string lobbyId, CancellationTokenSource cts)
        {
            try
            {
                string address = await StunUtility.TryResolvePublicAddressAsync(cts.Token);
                if (string.IsNullOrEmpty(address))
                {
                    LogWarning("[YargNetworkManager] STUN lookup did not return a public address.");
                    return;
                }

                await UniTask.SwitchToMainThread();

                if (_currentLobby == null || string.IsNullOrEmpty(_currentLobby.lobbyId) || !string.Equals(_currentLobby.lobbyId, lobbyId, StringComparison.Ordinal))
                {
                    return;
                }

                bool changed = false;

                if (!string.Equals(_currentLobby.publicAddress, address, StringComparison.OrdinalIgnoreCase))
                {
                    _currentLobby.publicAddress = address;
                    changed = true;
                }

                if (transportPort > 0 && _currentLobby.publicPort != transportPort)
                {
                    _currentLobby.publicPort = transportPort;
                    changed = true;
                }

                if (!changed)
                {
                    return;
                }

                LogInfo($"[YargNetworkManager] Resolved public endpoint via STUN: {address}:{transportPort}");

                var discovery = GetComponent<YargNetworkDiscovery>();
                discovery?.AdvertiseServer(_currentLobby);

                TriggerLobbyJoinedEvent(_currentLobby);
            }
            catch (OperationCanceledException)
            {
                LogInfo("[YargNetworkManager] Public endpoint resolution canceled.");
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Failed to resolve public endpoint: {ex.Message}");
            }
            finally
            {
                if (_publicEndpointResolveCts == cts)
                {
                    _publicEndpointResolveCts = null;
                }

                cts.Dispose();
            }
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

                var udpPort = transportPort;
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

        private void RegisterProbeHandlers()
        {
            NetworkClient.UnregisterHandler<LobbyProbeResponseMessage>();
            NetworkClient.RegisterHandler<LobbyProbeResponseMessage>(HandleLobbyProbeResponse, false);
        }

        private void HandleLobbyProbeRequest(NetworkConnectionToClient conn, LobbyProbeRequestMessage request)
        {
            try
            {
                var lobby = _currentLobby;
                LobbyProbeResponseMessage response;

                if (lobby == null || !_isHost)
                {
                    response = new LobbyProbeResponseMessage { success = false };
                }
                else
                {
                    response = new LobbyProbeResponseMessage
                    {
                        success = true,
                        lobbyId = lobby.lobbyId ?? string.Empty,
                        lobbyName = lobby.lobbyName ?? string.Empty,
                        hostName = lobby.hostName ?? string.Empty,
                        publicAddress = lobby.publicAddress ?? lobby.ipAddress ?? networkAddress,
                        transportId = lobby.transportId ?? (Transport.active != null ? Transport.active.GetType().Name : "Unknown"),
                        currentPlayers = Mathf.Clamp(GetTrackedPlayerCount(), 0, maxPlayers),
                        maxPlayers = lobby.maxPlayers,
                        hasPassword = lobby.hasPassword,
                        privacyMode = (int)lobby.privacyMode,
                        port = (ushort)Mathf.Clamp(lobby.port > 0 ? lobby.port : ResolveTransportPort(), 0, ushort.MaxValue),
                        publicPort = (ushort)Mathf.Clamp(lobby.publicPort > 0 ? lobby.publicPort : lobby.port, 0, ushort.MaxValue),
                        playerNames = lobby.playerNames ?? Array.Empty<string>(),
                        playerInstruments = lobby.playerInstruments ?? Array.Empty<int>()
                    };
                }

                conn.Send(response);
            }
            catch (Exception ex)
            {
                LogWarning($"[YargNetworkManager] Exception while handling probe request from {conn?.address}: {ex.Message}");
                try
                {
                    conn?.Send(new LobbyProbeResponseMessage { success = false });
                }
                catch (Exception sendEx)
                {
                    LogWarning($"[YargNetworkManager] Failed to send probe failure response: {sendEx.Message}");
                }
            }
            finally
            {
                MarkConnectionAsProbe(conn);

                try
                {
                    conn?.Disconnect();
                }
                catch (Exception disconnectEx)
                {
                    LogWarning($"[YargNetworkManager] Exception while disconnecting probe client: {disconnectEx.Message}");
                }
            }
        }

        private void HandleLobbyProbeResponse(LobbyProbeResponseMessage response)
        {
            if (_probeCompletionSource == null)
            {
                LogWarning("[YargNetworkManager] Received lobby probe response without an active probe request.");
                return;
            }

            if (!response.success)
            {
                CompleteProbe(null, ProbeCompletionState.Failed);
            }
            else
            {
                var info = new LobbyInfo
                {
                    lobbyId = response.lobbyId,
                    lobbyName = response.lobbyName,
                    hostName = response.hostName,
                    publicAddress = response.publicAddress,
                    ipAddress = response.publicAddress,
                    transportId = response.transportId,
                    currentPlayers = response.currentPlayers,
                    maxPlayers = response.maxPlayers,
                    hasPassword = response.hasPassword,
                    privacyMode = (LobbyPrivacyMode)Mathf.Clamp(response.privacyMode, 0, (int)LobbyPrivacyMode.Private),
                    port = response.port,
                    publicPort = response.publicPort,
                    isActive = true,
                    lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    playerNames = response.playerNames,
                    playerInstruments = response.playerInstruments
                };

                CompleteProbe(info, ProbeCompletionState.Success);
            }

            if (_activeProbeSessionId != 0 && _activeProbeSessionId == _activeClientSessionId && NetworkClient.isConnected)
            {
                NetworkClient.Disconnect();
            }
        }

        private void CompleteProbe(LobbyInfo info, ProbeCompletionState state)
        {
            var tcs = _probeCompletionSource;
            if (tcs == null)
            {
                return;
            }

            _probeCompletionSource = null;
            _probeHasCompleted = true;

            switch (state)
            {
                case ProbeCompletionState.Success:
                    tcs.TrySetResult(info);
                    break;
                case ProbeCompletionState.Canceled:
                    tcs.TrySetCanceled();
                    break;
                default:
                    tcs.TrySetResult(null);
                    break;
            }
        }

        private void FinalizeProbeConnection(int sessionId = 0)
        {
            try
            {
                _probeCancellationRegistration.Dispose();
            }
            catch
            {
                // Ignore disposal race during shutdown.
            }

            _probeCancellationRegistration = default;
            _probeConnectionPending = false;
            _probeConnectionActive = false;
            _probeHasCompleted = false;

            bool allowRestore = sessionId == 0 || _activeClientSessionId == 0 || _activeClientSessionId == sessionId;

            if (allowRestore)
            {
                if (!string.IsNullOrEmpty(_probePreviousAddress))
                {
                    networkAddress = _probePreviousAddress;
                }

                if (_probePreviousPort > 0)
                {
                    SetTransportPort(_probePreviousPort);
                }
            }

            _probePreviousAddress = string.Empty;
            _probePreviousPort = 0;

            if (sessionId == 0 || _activeProbeSessionId == sessionId)
            {
                _activeProbeSessionId = 0;
            }

            if (sessionId != 0 && _activeClientSessionId == sessionId)
            {
                _activeClientSessionId = 0;
            }
        }

        private int StartClientAndTrack()
        {
            int sessionId = unchecked(++_clientSessionCounter);
            _activeClientSessionId = sessionId;
            base.StartClient();
            return sessionId;
        }

        private void CancelActiveProbe(string reason)
        {
            if (!_probeConnectionPending && !_probeConnectionActive && _probeCompletionSource == null)
            {
                return;
            }

            LogInfo($"[YargNetworkManager] Canceling active probe: {reason}");

            if (_probeCompletionSource != null)
            {
                CompleteProbe(null, ProbeCompletionState.Canceled);
            }

            try
            {
                _probeCancellationRegistration.Dispose();
            }
            catch
            {
                // Ignore disposal race.
            }

            _probeCancellationRegistration = default;

            if (_activeClientSessionId == _activeProbeSessionId && NetworkClient.isConnected)
            {
                try
                {
                    _suppressClientDisconnectNotification = true;
                    NetworkClient.Disconnect();
                }
                catch (Exception disconnectEx)
                {
                    LogWarning($"[YargNetworkManager] Exception while disconnecting canceled probe client: {disconnectEx.Message}");
                }
            }

            StopActiveClient("Canceled probe", _activeProbeSessionId);

            _probeConnectionPending = false;
            _probeConnectionActive = false;
            _probeHasCompleted = false;

            FinalizeProbeConnection(_activeProbeSessionId);
        }

        private void StopActiveClient(string reason, int sessionId = 0)
        {
            if (sessionId != 0 && _activeClientSessionId != sessionId)
            {
                if (NetworkClient.active)
                {
                    LogInfo($"[YargNetworkManager] StopClient skipped for session mismatch (reason: {reason}). Active session: {_activeClientSessionId}, requested: {sessionId}");
                }
                return;
            }

            if (!NetworkClient.active)
            {
                return;
            }

            if (NetworkServer.active)
            {
                LogInfo($"[YargNetworkManager] StopClient skipped while server is active (reason: {reason}).");
                return;
            }

            LogInfo($"[YargNetworkManager] StopClient requested: {reason}");
            _clientStopPending = true;
            _activeClientSessionId = 0;
            StopClient();
        }

        private bool IsProbeConnection(NetworkConnectionToClient conn)
        {
            if (conn == null)
            {
                return false;
            }

            if (_probeConnectionIds.Contains(conn.connectionId))
            {
                return true;
            }

            return ReferenceEquals(conn.authenticationData, ProbeConnectionToken);
        }

        private void MarkConnectionAsProbe(NetworkConnectionToClient conn)
        {
            if (conn == null)
            {
                return;
            }

            conn.authenticationData = ProbeConnectionToken;

            _probeConnectionIds.Add(conn.connectionId);

            if (_connectedPlayers.Remove(conn))
            {
                UpdateCurrentLobbyPlayerCount();
            }
        }

        private void UpdateCurrentLobbyPlayerCount()
        {
            if (_currentLobby == null)
            {
                return;
            }

            _currentLobby.currentPlayers = Mathf.Clamp(GetTrackedPlayerCount(), 0, maxPlayers);
        }

        private int GetTrackedPlayerCount()
        {
            int totalPlayers = 0;

            foreach (var kvp in _connectedPlayers)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                totalPlayers += kvp.Value.Count;
            }

            return totalPlayers;
        }

        private bool ConnectionHasActivePlayers(NetworkConnectionToClient conn)
        {
            if (conn == null || !conn.isAuthenticated)
            {
                return false;
            }

            if (conn.identity != null)
            {
                return true;
            }

            if (!_connectedPlayers.TryGetValue(conn, out var list) || list == null)
            {
                return false;
            }

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null)
                {
                    return true;
                }
            }

            return false;
        }

        private NetworkConnectionToClient FindConnectionById(int connectionId)
        {
            if (connectionId < 0)
            {
                return null;
            }

            foreach (var kvp in _connectedPlayers)
            {
                if (kvp.Key != null && kvp.Key.connectionId == connectionId)
                {
                    return kvp.Key;
                }
            }

            if (NetworkServer.active && NetworkServer.connections.TryGetValue(connectionId, out var directConn))
            {
                return directConn;
            }

            return null;
        }

        private void RefreshHostOwnership()
        {
            NetworkConnectionToClient hostConnection = DetermineHostConnection();
            int newHostId = hostConnection?.connectionId ?? -1;
            bool hostChanged = _hostConnectionId != newHostId;

            if (hostChanged)
            {
                if (newHostId >= 0)
                {
                    LogInfo($"[YargNetworkManager] Host connection reassigned to {newHostId}.");
                }
                else
                {
                    LogInfo("[YargNetworkManager] Host connection cleared; no eligible players remain.");
                }

                _hostConnectionId = newHostId;
            }

            string resolvedHostName = string.Empty;

            foreach (var kvp in _connectedPlayers)
            {
                bool isHostConnection = hostConnection != null && kvp.Key == hostConnection;

                if (kvp.Value == null)
                {
                    continue;
                }

                foreach (var player in kvp.Value)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    player.SetIsHostServer(isHostConnection);

                    if (isHostConnection && string.IsNullOrWhiteSpace(resolvedHostName) && !string.IsNullOrWhiteSpace(player.PlayerName))
                    {
                        resolvedHostName = player.PlayerName;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(resolvedHostName) && hostConnection != null)
            {
                resolvedHostName = _playerName;
            }

            if (_currentLobby != null && !string.IsNullOrWhiteSpace(resolvedHostName))
            {
                if (!string.Equals(_currentLobby.hostName, resolvedHostName, StringComparison.Ordinal))
                {
                    _currentLobby.hostName = resolvedHostName;
                    if (NetworkServer.active)
                    {
                        _currentLobby.currentPlayers = Mathf.Clamp(GetTotalPlayerCount(), 0, _currentLobby.maxPlayers);
                        BroadcastLobbyInfoSnapshot();
                    }
                }
            }
            else if (_currentLobby != null && hostConnection == null)
            {
                string fallbackHost = _playerName;
                if (!string.Equals(_currentLobby.hostName, fallbackHost, StringComparison.Ordinal))
                {
                    _currentLobby.hostName = fallbackHost;
                    if (NetworkServer.active)
                    {
                        _currentLobby.currentPlayers = Mathf.Clamp(GetTotalPlayerCount(), 0, _currentLobby.maxPlayers);
                        BroadcastLobbyInfoSnapshot();
                    }
                }
            }
        }

        internal bool ConnectionIsHost(NetworkConnectionToClient conn)
        {
            if (NetworkServer.active && conn is LocalConnectionToClient)
            {
                return _isHost;
            }

            if (conn == null)
            {
                return NetworkServer.active && _isHost;
            }

            if (conn.connectionId == _hostConnectionId)
            {
                return true;
            }

            var resolved = FindConnectionById(_hostConnectionId);
            if (resolved != null && resolved == conn)
            {
                return true;
            }

            return false;
        }

        private NetworkConnectionToClient DetermineHostConnection()
        {
            NetworkConnectionToClient localHost = null;
            NetworkConnectionToClient firstRemote = null;

            foreach (var kvp in _connectedPlayers)
            {
                NetworkConnectionToClient conn = kvp.Key;
                List<NetworkPlayerData> players = kvp.Value;

                if (conn == null || players == null || players.Count == 0)
                {
                    continue;
                }

                bool hasValidPlayer = false;
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] != null)
                    {
                        hasValidPlayer = true;
                        break;
                    }
                }

                if (!hasValidPlayer)
                {
                    continue;
                }

                if (conn is LocalConnectionToClient)
                {
                    localHost = conn;
                    break;
                }

                if (firstRemote == null || conn.connectionId < firstRemote.connectionId)
                {
                    firstRemote = conn;
                }
            }

            return localHost ?? firstRemote;
        }

        private void BroadcastLobbyInfoSnapshot()
        {
            if (!NetworkServer.active || _currentLobby == null)
            {
                return;
            }

            int privacy = (int)_currentLobby.privacyMode;

            foreach (var kvp in _connectedPlayers)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                foreach (var player in kvp.Value)
                {
                    if (player == null)
                    {
                        continue;
                    }

                    player.TargetSyncLobbyInfo(
                        _currentLobby.lobbyName,
                        _currentLobby.hostName,
                        _currentLobby.maxPlayers,
                        _currentLobby.hasPassword,
                        privacy,
                        _currentLobby.currentPlayers);
                }
            }
        }

        internal void ServerOnPlayerRenamed(NetworkPlayerData playerData)
        {
            if (!NetworkServer.active || playerData == null || _currentLobby == null)
            {
                return;
            }

            if (playerData.IsHost && !string.IsNullOrWhiteSpace(playerData.PlayerName))
            {
                if (!string.Equals(_currentLobby.hostName, playerData.PlayerName, StringComparison.Ordinal))
                {
                    _currentLobby.hostName = playerData.PlayerName;
                    _currentLobby.currentPlayers = Mathf.Clamp(GetTotalPlayerCount(), 0, _currentLobby.maxPlayers);
                    BroadcastLobbyInfoSnapshot();
                }
            }
        }

        #region Mirror Callbacks

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.UnregisterHandler<LobbyProbeRequestMessage>();
            NetworkServer.RegisterHandler<LobbyProbeRequestMessage>(HandleLobbyProbeRequest, false);
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

            if (IsProbeConnection(conn))
            {
                _probeConnectionIds.Add(conn.connectionId);
            }

            // Initialize player list for this connection
            if (!_connectedPlayers.ContainsKey(conn))
            {
                _connectedPlayers[conn] = new List<NetworkPlayerData>();
            }

            UpdateCurrentLobbyPlayerCount();

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

            bool wasAuthenticated = conn != null && conn.isAuthenticated;
            bool isProbe = IsProbeConnection(conn);

            // Get player name before removing (only meaningful for authenticated connections)
            string disconnectedPlayerName = null;
            bool hadPlayerData = false;
            if (wasAuthenticated && _connectedPlayers.ContainsKey(conn))
            {
                var players = _connectedPlayers[conn];
                if (players.Count > 0 && players[0] != null)
                {
                    disconnectedPlayerName = players[0].PlayerName;
                    hadPlayerData = true;
                }
                else
                {
                    disconnectedPlayerName = "Unknown";
                }
            }
            
            // Remove all players from this connection
            if (_connectedPlayers.ContainsKey(conn))
            {
                foreach (var player in _connectedPlayers[conn])
                {
                    RemoveSongLibraryForPlayer(player);
                    OnPlayerLeft?.Invoke(player);
                }
                _connectedPlayers.Remove(conn);
            }

            UpdateCurrentLobbyPlayerCount();

            if (isProbe)
            {
                if (conn != null)
                {
                    _probeConnectionIds.Remove(conn.connectionId);
                }
                base.OnServerDisconnect(conn);
                LogInfo($"[YargNetworkManager] Probe connection {conn?.connectionId} closed.");
                return;
            }
            
            if (!wasAuthenticated || !hadPlayerData)
            {
                LogInfo($"[YargNetworkManager] Connection {conn.connectionId} failed authentication or disconnected before completing setup; skipping player left notifications.");
            }
            else
            {
                // Show toast notification for player disconnect (except when host disconnects everyone)
                if (NetworkServer.active && conn.connectionId != 0 && !string.IsNullOrEmpty(disconnectedPlayerName))
                {
                    var allPlayers = GetAllPlayers();
                    if (allPlayers.Count > 0 && allPlayers[0] != null)
                    {
                        allPlayers[0].RpcShowPlayerLeftToast(disconnectedPlayerName);
                    }
                }
            }

            OnClientDisconnected?.Invoke(conn);
            base.OnServerDisconnect(conn);
            LogInfo($"Client disconnected: {conn.connectionId}");

            if (conn != null)
            {
                _probeConnectionIds.Remove(conn.connectionId);
            }

            RefreshHostOwnership();
            RecalculateSharedSongs();
        }

        private bool _hasTriggeredJoinEvent = false;

        public override void OnClientConnect()
        {
            if (_probeConnectionPending)
            {
                _probeConnectionPending = false;
                _probeConnectionActive = true;

                try
                {
                    var request = new LobbyProbeRequestMessage
                    {
                        clientVersion = Application.version
                    };
                    NetworkClient.Send(request);
                    LogInfo($"[YargNetworkManager] Sent lobby probe request to {networkAddress}:{ResolveTransportPort()}");
                }
                catch (Exception ex)
                {
                    LogWarning($"[YargNetworkManager] Failed to send lobby probe request: {ex.Message}");
                    CompleteProbe(null, ProbeCompletionState.Failed);
                    if (_activeProbeSessionId != 0 && _activeProbeSessionId == _activeClientSessionId && NetworkClient.isConnected)
                    {
                        NetworkClient.Disconnect();
                    }
                }

                return;
            }

            base.OnClientConnect();

            if (_isTransitioningToHost)
            {
                _isTransitioningToHost = false;
                LogInfo("[YargNetworkManager] Host transition complete; client reconnected.");
            }

            LogInfo($"[YargNetworkManager] Successfully connected to host at {networkAddress}!");

            _clientJoinPending = false;

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
            if (_suppressClientDisconnectNotification)
            {
                _suppressClientDisconnectNotification = false;
                base.OnClientDisconnect();
                LogInfo("[YargNetworkManager] Suppressed client disconnect notification after probe cancellation.");
                return;
            }

            if (_probeConnectionActive || _probeCompletionSource != null || _probeHasCompleted)
            {
                base.OnClientDisconnect();

                if (!_probeHasCompleted)
                {
                    CompleteProbe(null, ProbeCompletionState.Failed);
                }

                FinalizeProbeConnection(_activeProbeSessionId);
                return;
            }

            base.OnClientDisconnect();

            if (!NetworkClient.active)
            {
                _activeClientSessionId = 0;
            }

            PasswordAuthenticator.HandleClientDisconnectFallback();

            // Skip cleanup during application shutdown to prevent menu navigation errors
            if (_isQuitting)
            {
                LogInfo("[YargNetworkManager] Application is quitting, skipping OnClientDisconnect cleanup");
                return;
            }

            if (_isTransitioningToHost)
            {
                _isTransitioningToHost = false;
                LogInfo("[YargNetworkManager] Ignoring client disconnect cleanup during host transition.");
                return;
            }

            if (_clientStopPending)
            {
                _clientStopPending = false;
                LogInfo("[YargNetworkManager] Client disconnect associated with StopClient; skipping lobby teardown.");
                return;
            }

            _currentLobby = null;
            _connectedPlayers.Clear();
            _hostConnectionId = -1;
            _hasTriggeredJoinEvent = false;
            _clientJoinPending = false;
            ResetJoinTracking();
            ResetSharedSongState();

            OnLobbyLeft?.Invoke();
            LogInfo("Disconnected from host");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Mirror clears spawn handlers on shutdown; prepare for the next connection immediately.
            RegisterMultiplayerShowPlaylistSpawnHandler();

            _localSlotSyncPending = false;
            ResetSharedSongState();
            _probeConnectionIds.Clear();
            _clientStopPending = false;
            _clientNotifiedPlayers.Clear();
            _activeClientSessionId = 0;
            _activeProbeSessionId = 0;
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            TeardownPortMappingsAsync().Forget();
            ResetSharedSongState();
            _probeConnectionIds.Clear();
            _hostConnectionId = -1;
            _isDedicatedServer = false;
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            TeardownPortMappingsAsync().Forget();
            ResetSharedSongState();
            _probeConnectionIds.Clear();
            _hostConnectionId = -1;
            _isDedicatedServer = false;
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
                
                // Add to connected players list
                if (_connectedPlayers.ContainsKey(conn))
                {
                    _connectedPlayers[conn].Add(playerData);
                }
                else
                {
                    LogWarning($"[YargNetworkManager] Connection {conn.connectionId} not found in _connectedPlayers dictionary");
                }

                UpdateCurrentLobbyPlayerCount();

                OnPlayerJoined?.Invoke(playerData);
                LogInfo($"[YargNetworkManager] Player spawned successfully for connection {conn.connectionId}");

                TrackPlayerSongLibrary(playerData);
                
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
                    int currentPlayers = Mathf.Clamp(_currentLobby.currentPlayers, 0, _currentLobby.maxPlayers);
                    playerData.TargetSyncLobbyInfo(_currentLobby.lobbyName, _currentLobby.hostName,
                        _currentLobby.maxPlayers, _currentLobby.hasPassword, (int)_currentLobby.privacyMode, currentPlayers);
                }
                RefreshHostOwnership();
            }
            else
            {
                LogError($"[YargNetworkManager] Player prefab does not have NetworkPlayerData component!");
            }
        }

        internal void ServerRegisterSongLibraryChunk(NetworkPlayerData playerData, byte[] chunk, bool isFirstChunk, bool isFinalChunk)
        {
            if (!NetworkServer.active || playerData == null)
            {
                return;
            }

            uint netId = playerData.netId;

            if (isFirstChunk || !_playerSongLibraries.ContainsKey(netId))
            {
                _playerSongLibraries[netId] = new HashSet<HashWrapper>();
                _playersPendingSongSync.Add(netId);
                UpdateSharedSongSyncState();

                if (!_songLibraryReceiveTimers.TryGetValue(netId, out var timer))
                {
                    timer = new Stopwatch();
                    _songLibraryReceiveTimers[netId] = timer;
                }

                timer.Restart();
                _songLibraryChunkCounts[netId] = 0;
                _songLibraryReceivedHashes[netId] = 0;
                _songLibraryReceivedBytes[netId] = 0;

                if (_songLibraryFirstChunkTimers.TryGetValue(netId, out var firstChunkTimer) && firstChunkTimer.IsRunning)
                {
                    firstChunkTimer.Stop();
                    LogInfo($"[SongSync] Player {netId} first chunk arrived after {firstChunkTimer.Elapsed.TotalMilliseconds:F2} ms since library tracking.");
                }

                LogInfo($"[SongSync] Player {netId} started uploading song hashes.");
            }

            var library = _playerSongLibraries[netId];

            const int headerSize = 5;
            int hashSize = HashWrapper.HASH_SIZE_IN_BYTES;

            int processedHashes = 0;
            int rawLength = 0;
            int totalRead = 0;
            bool isCompressed = false;
            double chunkElapsedMs = 0d;

            if (chunk.Length > 0)
            {
                var chunkStopwatch = Stopwatch.StartNew();

                if (chunk.Length < headerSize)
                {
                    LogWarning($"[YargNetworkManager] Song library chunk from player {netId} was too small ({chunk.Length}).");
                }
                else
                {
                    isCompressed = chunk[0] == 1;
                    rawLength = BinaryPrimitives.ReadInt32LittleEndian(chunk.AsSpan(1, 4));
                    totalRead = rawLength;

                    if (rawLength < 0)
                    {
                        LogWarning($"[YargNetworkManager] Song library chunk from player {netId} had negative payload length.");
                    }
                    else if (rawLength == 0)
                    {
                        // No hashes in this chunk.
                    }
                    else
                    {
                        ReadOnlySpan<byte> payloadSpan = ReadOnlySpan<byte>.Empty;
                        byte[] rentedBuffer = null;

                        try
                        {
                            if (isCompressed)
                            {
                                rentedBuffer = ArrayPool<byte>.Shared.Rent(Math.Max(rawLength, 1));
                                totalRead = 0;

                                try
                                {
                                    using var compressedStream = new MemoryStream(chunk, headerSize, chunk.Length - headerSize);
                                    using var deflate = new DeflateStream(compressedStream, CompressionMode.Decompress);

                                    while (totalRead < rawLength)
                                    {
                                        int read = deflate.Read(rentedBuffer, totalRead, rawLength - totalRead);
                                        if (read == 0)
                                        {
                                            break;
                                        }
                                        totalRead += read;
                                    }
                                }
                                catch (Exception ex)
                                {
                                    LogWarning($"[YargNetworkManager] Failed to decompress song library chunk from player {netId}: {ex.Message}");
                                    totalRead = 0;
                                }

                                payloadSpan = new ReadOnlySpan<byte>(rentedBuffer, 0, Math.Max(totalRead, 0));
                            }
                            else
                            {
                                int availableBytes = Math.Min(rawLength, chunk.Length - headerSize);
                                payloadSpan = new ReadOnlySpan<byte>(chunk, headerSize, Math.Max(availableBytes, 0));

                                if (availableBytes != rawLength)
                                {
                                    LogWarning($"[YargNetworkManager] Song library chunk from player {netId} expected {rawLength} bytes but received {availableBytes}.");
                                    totalRead = availableBytes;
                                }
                            }

                            if (totalRead % hashSize != 0)
                            {
                                LogWarning($"[YargNetworkManager] Song library chunk from player {netId} had misaligned data ({totalRead} bytes).");
                            }
                            else
                            {
                                processedHashes = totalRead / hashSize;

                                for (int offset = 0; offset < totalRead; offset += hashSize)
                                {
                                    var hash = HashWrapper.Create(payloadSpan.Slice(offset, hashSize));
                                    library.Add(hash);
                                }
                            }
                        }
                        finally
                        {
                            if (rentedBuffer != null)
                            {
                                ArrayPool<byte>.Shared.Return(rentedBuffer);
                            }
                        }
                    }
                }

                chunkStopwatch.Stop();
                chunkElapsedMs = chunkStopwatch.Elapsed.TotalMilliseconds;
            }

            _songLibraryChunkCounts.TryGetValue(netId, out int previousChunkCount);
            int currentChunk = previousChunkCount + 1;
            _songLibraryChunkCounts[netId] = currentChunk;

            if (processedHashes > 0)
            {
                _songLibraryReceivedHashes.TryGetValue(netId, out int previousHashes);
                _songLibraryReceivedHashes[netId] = previousHashes + processedHashes;

                _songLibraryReceivedBytes.TryGetValue(netId, out long previousBytes);
                _songLibraryReceivedBytes[netId] = previousBytes + (long)processedHashes * hashSize;
            }

            if (chunk.Length > 0)
            {
                LogInfo($"[SongSync] Player {netId}: chunk {currentChunk} processed {processedHashes} hashes (raw: {rawLength} bytes, decompressed: {totalRead} bytes, compressed: {isCompressed}) in {chunkElapsedMs:F2} ms.");
            }
            else
            {
                LogInfo($"[SongSync] Player {netId}: chunk {currentChunk} contained no payload (isFirst:{isFirstChunk}, isFinal:{isFinalChunk}).");
            }

            if (isFinalChunk)
            {
                _playersPendingSongSync.Remove(netId);

                _songLibraryReceiveTimers.TryGetValue(netId, out var overallTimer);
                if (overallTimer != null)
                {
                    overallTimer.Stop();
                }

                _songLibraryChunkCounts.TryGetValue(netId, out int totalChunks);
                _songLibraryReceivedHashes.TryGetValue(netId, out int totalHashes);
                _songLibraryReceivedBytes.TryGetValue(netId, out long totalBytes);

                double totalMs = overallTimer?.Elapsed.TotalMilliseconds ?? 0d;
                float totalKiB = totalBytes / 1024f;
                LogInfo($"[SongSync] Player {netId} upload complete in {totalMs:F2} ms ({totalHashes} hashes, {totalKiB:F1} KiB across {totalChunks} chunks). Library now stores {library.Count} unique hashes.");

                _songLibraryReceiveTimers.Remove(netId);
                _songLibraryChunkCounts.Remove(netId);
                _songLibraryReceivedHashes.Remove(netId);
                _songLibraryReceivedBytes.Remove(netId);

                RecalculateSharedSongs();
            }
        }

        private void TrackPlayerSongLibrary(NetworkPlayerData playerData)
        {
            if (!NetworkServer.active || playerData == null)
            {
                return;
            }

            uint netId = playerData.netId;

            _playersPendingSongSync.Add(netId);
            _playerSongLibraries[netId] = new HashSet<HashWrapper>();
            UpdateSharedSongSyncState();

            if (!_songLibraryFirstChunkTimers.TryGetValue(netId, out var timer))
            {
                timer = new Stopwatch();
                _songLibraryFirstChunkTimers[netId] = timer;
            }

            timer.Restart();
        }

        private void RemoveSongLibraryForPlayer(NetworkPlayerData playerData)
        {
            if (playerData == null)
            {
                return;
            }

            _playerSongLibraries.Remove(playerData.netId);
            _playersPendingSongSync.Remove(playerData.netId);
            _songLibraryReceiveTimers.Remove(playerData.netId);
            _songLibraryFirstChunkTimers.Remove(playerData.netId);
            _songLibraryChunkCounts.Remove(playerData.netId);
            _songLibraryReceivedHashes.Remove(playerData.netId);
            _songLibraryReceivedBytes.Remove(playerData.netId);
            UpdateSharedSongSyncState();
        }

        private void UpdateSharedSongSyncState()
        {
            bool isComplete = _playersPendingSongSync.Count == 0;

            if (isComplete != _sharedSongSyncComplete)
            {
                _sharedSongSyncComplete = isComplete;
                OnSharedSongSyncStateChanged?.Invoke(isComplete);
            }

            if (isComplete && _pendingSongSelectionBroadcast)
            {
                _pendingSongSelectionBroadcast = false;
                BroadcastSongSelectionNavigation();
            }
        }

        private void RecalculateSharedSongs()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            if (_playersPendingSongSync.Count > 0)
            {
                return;
            }

            int playerCount = _playerSongLibraries.Count;

            if (playerCount == 0)
            {
                _sharedSongHashes = null;
                BroadcastSharedSongs();
                UpdateSharedSongSyncState();
                LogInfo("[SongSync] Shared song intersection skipped (no player libraries available).");
                return;
            }

            var recalcTimer = Stopwatch.StartNew();
            HashSet<HashWrapper>? intersection = null;
            foreach (var library in _playerSongLibraries.Values)
            {
                if (intersection == null)
                {
                    intersection = new HashSet<HashWrapper>(library);
                }
                else
                {
                    intersection.IntersectWith(library);
                }

                if (intersection.Count == 0)
                {
                    break;
                }
            }

            _sharedSongHashes = intersection ?? new HashSet<HashWrapper>();
            recalcTimer.Stop();
            int sharedCount = _sharedSongHashes.Count;
            LogInfo($"[SongSync] Shared song intersection for {playerCount} players computed in {recalcTimer.Elapsed.TotalMilliseconds:F2} ms (shared hashes: {sharedCount}).");
            BroadcastSharedSongs();
            UpdateSharedSongSyncState();
        }

        private void BroadcastSharedSongs()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            var broadcastTimer = Stopwatch.StartNew();

            var players = GetAllPlayers();
            int targetPlayerCount = 0;
            foreach (var player in players)
            {
                if (player != null)
                {
                    targetPlayerCount++;
                }
            }

            if (_sharedSongHashes == null)
            {
                foreach (var player in players)
                {
                    player?.TargetClearSharedSongs();
                }

                MultiplayerSongFilter.ClearSharedSongs();
                broadcastTimer.Stop();
                LogInfo($"[SongSync] Cleared shared songs for {targetPlayerCount} players in {broadcastTimer.Elapsed.TotalMilliseconds:F2} ms.");
                return;
            }

            var chunkBuildTimer = Stopwatch.StartNew();
            var chunks = BuildSharedSongChunks(_sharedSongHashes);
            chunkBuildTimer.Stop();

            int totalChunks = chunks.Count;
            int totalBytes = 0;
            foreach (var chunk in chunks)
            {
                totalBytes += chunk.Length;
            }

            LogInfo($"[SongSync] Prepared {totalChunks} shared-song chunks ({_sharedSongHashes.Count} hashes, {totalBytes / 1024f:F1} KiB) in {chunkBuildTimer.Elapsed.TotalMilliseconds:F2} ms.");

            foreach (var player in players)
            {
                if (player == null)
                {
                    continue;
                }

                bool isFirstChunk = true;
                for (int i = 0; i < chunks.Count; i++)
                {
                    bool isFinalChunk = i == chunks.Count - 1;
                    player.TargetReceiveSharedSongChunk(chunks[i], isFirstChunk, isFinalChunk);
                    isFirstChunk = false;
                }
            }

            broadcastTimer.Stop();
            LogInfo($"[SongSync] Broadcast shared songs to {targetPlayerCount} players in {broadcastTimer.Elapsed.TotalMilliseconds:F2} ms.");
        }

        private static List<byte[]> BuildSharedSongChunks(HashSet<HashWrapper> hashes)
        {
            const int maxChunkBytes = 32768;
            int hashSize = HashWrapper.HASH_SIZE_IN_BYTES;
            int hashesPerChunk = Math.Max(1, maxChunkBytes / hashSize);

            if (hashes.Count == 0)
            {
                return new List<byte[]> { Array.Empty<byte>() };
            }

            var hashArray = hashes.ToArray();
            var chunks = new List<byte[]>();
            int index = 0;

            while (index < hashArray.Length)
            {
                int chunkCount = Math.Min(hashesPerChunk, hashArray.Length - index);
                using var stream = new MemoryStream(chunkCount * hashSize);
                for (int i = 0; i < chunkCount; i++)
                {
                    hashArray[index + i].Serialize(stream);
                }

                chunks.Add(stream.ToArray());
                index += chunkCount;
            }

            return chunks;
        }

        private void ResetSharedSongState()
        {
            _playerSongLibraries.Clear();
            _playersPendingSongSync.Clear();
            _sharedSongHashes = null;
            _pendingSongSelectionBroadcast = false;
            _songLibraryReceiveTimers.Clear();
            _songLibraryFirstChunkTimers.Clear();
            _songLibraryChunkCounts.Clear();
            _songLibraryReceivedHashes.Clear();
            _songLibraryReceivedBytes.Clear();
            MultiplayerSongFilter.ClearSharedSongs();
            UpdateSharedSongSyncState();
        }

        public override void OnServerError(NetworkConnectionToClient conn, TransportError error, string reason)
        {
            base.OnServerError(conn, error, reason);
            LogError($"[YargNetworkManager] Server error on connection {conn.connectionId}: {error} - {reason}");
            OnNetworkError?.Invoke($"Server error: {reason}");
        }

        private static string FormatClientErrorMessage(TransportError error, string reason)
        {
            switch (error)
            {
                case TransportError.Timeout:
                    return "Connection attempt timed out.";
                case TransportError.DnsResolve:
                    return "Could not resolve the server address.";
                case TransportError.Refused:
                    return "The host refused the connection.";
                case TransportError.Congestion:
                    return "Connection failed because the network is congested.";
                case TransportError.InvalidReceive:
                    return "Received an invalid response from the server.";
                case TransportError.InvalidSend:
                    return "Failed to send data to the server.";
                case TransportError.ConnectionClosed:
                    return "The connection closed unexpectedly.";
                case TransportError.Unexpected:
                    return string.IsNullOrWhiteSpace(reason) ? "An unexpected network error occurred." : reason.Trim();
            }

            if (!string.IsNullOrWhiteSpace(reason))
            {
                return reason.Trim();
            }

            return "Connection error.";
        }

        public override void OnClientError(TransportError error, string reason)
        {
            bool isProbe = _probeConnectionPending || _probeConnectionActive || _probeCompletionSource != null;

            base.OnClientError(error, reason);

            if (isProbe)
            {
                LogInfo($"[YargNetworkManager] Probe connection error suppressed: {error} - {reason}");
                if (!_probeHasCompleted)
                {
                    var state = error == TransportError.Timeout ? ProbeCompletionState.Timeout : ProbeCompletionState.Failed;
                    CompleteProbe(null, state);
                }

                return;
            }

            LogError($"[YargNetworkManager] Client error: {error} - {reason}");
            LogError($"[YargNetworkManager] Was trying to connect to: {networkAddress}");
            _clientJoinPending = false;
            if (!_isHost && NetworkClient.active)
            {
                StopClient();
            }

            ResetJoinTracking();

            var displayMessage = FormatClientErrorMessage(error, reason);
            bool suppressToast = PasswordAuthenticator.WasAuthFailureToastShown();
            if (!suppressToast)
            {
                ToastManager.ToastError(displayMessage);
            }

            PasswordAuthenticator.ClearPendingFailureState();
            OnNetworkError?.Invoke(displayMessage);
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

        public void RequestKickPlayer(NetworkPlayerData targetPlayer)
        {
            if (targetPlayer == null)
            {
                LogWarning("[YargNetworkManager] RequestKickPlayer called with null target.");
                return;
            }

            if (NetworkServer.active && _isHost)
            {
                KickPlayer(targetPlayer.connectionToClient);
                return;
            }

            if (!NetworkClient.active)
            {
                LogWarning("[YargNetworkManager] Cannot request kick; client is not active.");
                return;
            }

            var localPlayer = GetLocalPrimaryPlayerData();
            if (localPlayer == null)
            {
                LogWarning("[YargNetworkManager] Cannot request kick; no local player data found.");
                return;
            }

            if (!localPlayer.IsHost)
            {
                LogWarning("[YargNetworkManager] Local player is not host; kick request ignored.");
                return;
            }

            if (targetPlayer.connectionToClient == null)
            {
                LogWarning("[YargNetworkManager] Cannot request kick; target connection is invalid.");
                return;
            }

            localPlayer.CmdRequestKickPlayer(targetPlayer.netId);
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

            if (!_sharedSongSyncComplete)
            {
                LogInfo("[YargNetworkManager] Song selection requested, waiting for shared song sync to complete.");
                _pendingSongSelectionBroadcast = true;
                return;
            }

            BroadcastSongSelectionNavigation();
        }

        public void RequestStartSongSelection()
        {
            if (NetworkServer.active && _isHost)
            {
                StartSongSelection();
                return;
            }

            if (!NetworkClient.active)
            {
                LogWarning("[YargNetworkManager] Cannot request song selection; client is not active.");
                return;
            }

            var localPlayer = GetLocalPrimaryPlayerData();
            if (localPlayer == null)
            {
                LogWarning("[YargNetworkManager] Cannot request song selection; no local player data found.");
                return;
            }

            if (!localPlayer.IsHost)
            {
                LogWarning("[YargNetworkManager] Local player is not host; song selection request ignored.");
                return;
            }

            localPlayer.CmdRequestStartSongSelection();
        }

        public void RequestSyncMenuNavigation(bool popMenu, Menu.MenuManager.Menu targetMenu = Menu.MenuManager.Menu.None)
        {
            if (NetworkServer.active && _isHost)
            {
                SyncMenuNavigation(popMenu, targetMenu);
                return;
            }

            if (!NetworkClient.active)
            {
                LogWarning("[YargNetworkManager] Cannot request menu sync; client is not active.");
                return;
            }

            var localPlayer = GetLocalPrimaryPlayerData();
            if (localPlayer == null)
            {
                LogWarning("[YargNetworkManager] Cannot request menu sync; no local player data found.");
                return;
            }

            if (!localPlayer.IsHost)
            {
                LogWarning("[YargNetworkManager] Local player is not host; menu sync request ignored.");
                return;
            }

            localPlayer.CmdRequestSyncMenuNavigation(popMenu, (int)targetMenu);
        }

        [Server]
        private void BroadcastSongSelectionNavigation()
        {
            LogInfo("[YargNetworkManager] Host starting song selection for all clients");

            foreach (var playerData in GetAllPlayers())
            {
                if (playerData != null)
                {
                    playerData.TargetNavigateToMusicLibrary();
                }
            }

            OnHostStartedSongSelection?.Invoke();
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

            if (_multiplayerShowPlaylist == null)
            {
                _multiplayerShowPlaylist = FindObjectOfType<YARG.Multiplayer.MultiplayerShowPlaylist>();
                if (_multiplayerShowPlaylist == null)
                {
                    LogWarning("[YargNetworkManager] AdvanceAfterScoreScreen could not locate MultiplayerShowPlaylist instance.");
                }
            }

            var playlistComponent = _multiplayerShowPlaylist;
            var playlist = playlistComponent?.ShowPlaylist;
            int playlistCount = playlist?.Count ?? 0;

            int showIndex = GlobalVariables.State.ShowIndex;
            if (playlistCount > 0)
            {
                showIndex = Mathf.Clamp(showIndex, 0, playlistCount - 1);
            }
            else if (GlobalVariables.State.PlayingAShow &&
                     GlobalVariables.State.ShowSongs != null &&
                     GlobalVariables.State.ShowSongs.Count > 0)
            {
                showIndex = Mathf.Clamp(GlobalVariables.State.ShowIndex, 0, GlobalVariables.State.ShowSongs.Count - 1);
            }
            else
            {
                showIndex = Mathf.Max(0, showIndex);
            }

            HashWrapper currentHash = default;
            bool haveCurrentHash = false;

            if (playlistCount > 0 && showIndex >= 0 && showIndex < playlistCount)
            {
                currentHash = playlist.SongHashes[showIndex];
                haveCurrentHash = true;
            }
            else if (GlobalVariables.State.PlayingAShow &&
                     GlobalVariables.State.ShowSongs != null &&
                     GlobalVariables.State.ShowIndex >= 0 &&
                     GlobalVariables.State.ShowIndex < GlobalVariables.State.ShowSongs.Count &&
                     GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex] != null)
            {
                currentHash = GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex].Hash;
                haveCurrentHash = true;
            }

            bool removalSucceeded = false;

            if (haveCurrentHash && playlistComponent != null)
            {
                removalSucceeded = playlistComponent.HostRemoveSong(currentHash);
                if (!removalSucceeded)
                {
                    LogWarning($"[YargNetworkManager] Failed to remove completed show entry with hash {currentHash}.");
                }
            }
            else if (haveCurrentHash && GlobalVariables.State.ShowSongs != null &&
                     GlobalVariables.State.ShowSongs.Count > 0 &&
                     GlobalVariables.State.ShowIndex >= 0 &&
                     GlobalVariables.State.ShowIndex < GlobalVariables.State.ShowSongs.Count)
            {
                GlobalVariables.State.ShowSongs.RemoveAt(GlobalVariables.State.ShowIndex);
                removalSucceeded = true;
            }
            else
            {
                LogWarning("[YargNetworkManager] AdvanceAfterScoreScreen could not determine completed playlist entry; ending show.");
            }

            if (!removalSucceeded)
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

                return;
            }

            int updatedCount = playlistComponent?.ShowPlaylist?.Count ?? GlobalVariables.State.ShowSongs?.Count ?? 0;
            if (updatedCount > 0)
            {
                int nextIndex = Mathf.Clamp(showIndex, 0, updatedCount - 1);
                HashWrapper nextHash = default;
                bool haveNextHash = false;

                if (playlistComponent != null && playlistComponent.ShowPlaylist.Count > 0 &&
                    nextIndex >= 0 && nextIndex < playlistComponent.ShowPlaylist.Count)
                {
                    nextHash = playlistComponent.ShowPlaylist.SongHashes[nextIndex];
                    haveNextHash = true;
                }
                else if (GlobalVariables.State.ShowSongs != null &&
                         nextIndex >= 0 && nextIndex < GlobalVariables.State.ShowSongs.Count &&
                         GlobalVariables.State.ShowSongs[nextIndex] != null)
                {
                    nextHash = GlobalVariables.State.ShowSongs[nextIndex].Hash;
                    haveNextHash = true;
                }

                if (haveNextHash)
                {
                    SongEntry nextSong = null;
                    if (SongContainer.SongsByHash.TryGetValue(nextHash, out var nextList) && nextList.Count > 0)
                    {
                        nextSong = nextList[0];
                    }

                    GlobalVariables.State.PlayingAShow = true;
                    GlobalVariables.State.ShowIndex = nextIndex;
                    GlobalVariables.State.CurrentSong = nextSong;

                    foreach (var player in players)
                    {
                        player?.TargetBeginNextShowSong(nextHash.ToString(), nextIndex);
                    }
                }
                else
                {
                    LogWarning("[YargNetworkManager] AdvanceAfterScoreScreen could not resolve next song hash; ending show.");
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
            public int currentPlayers;
            public int maxPlayers;
            public LobbyPrivacyMode privacyMode;
            public bool hasPassword;
            public string password;
            public bool isActive;
            public int port;
            public int publicPort;
            public long lastSeen;

            // New fields for discovered player info
            public string[] playerNames;
            public int[] playerInstruments;

            public override string ToString()
            {
                return $"{lobbyName} ({currentPlayers}/{maxPlayers}) - Host: {hostName} @ {ipAddress}:{port}";
            }
        }

        public override void OnApplicationQuit()
        {
            _isQuitting = true;
            LogInfo("[YargNetworkManager] OnApplicationQuit called, setting _isQuitting = true");
            CancelPublicEndpointResolution();
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
            CancelPublicEndpointResolution();
            TeardownPortMappingsAsync().Forget();
            base.OnDestroy();
        }
    }
}