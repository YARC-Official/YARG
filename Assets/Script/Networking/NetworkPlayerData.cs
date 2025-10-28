using System;
using System.Collections;
using System.IO;
using System.Linq;
using Mirror;
using UnityEngine;
using YARG;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Multiplayer;

namespace YARG.Networking
{
    /// <summary>
    /// Represents a player in the network session, including lobby metadata and the
    /// aggregated gameplay snapshot used for local-authority multiplayer.
    /// </summary>
    public class NetworkPlayerData : NetworkBehaviour
    {
        private const int SCORE_DELTA_WARNING = 8000;
        private const int NOTES_DELTA_WARNING = 20;
        private const float LATENCY_WARNING_THRESHOLD_MS = 350f;
        private const double SNAPSHOT_OUT_OF_ORDER_LOG_COOLDOWN = 1.5d;
        private const int SONG_HASHES_PER_CHUNK = 2048;

        [Header("Player Info")]
        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        private string playerName = "Player";
        
        private void Awake()
        {
            // CRITICAL: Explicitly mark as DontDestroyOnLoad to survive scene transitions
            // Without this, Unity destroys the object when MenuScene unloads
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[NetworkPlayerData] {playerName} marked as DontDestroyOnLoad");
        }

        private void OnEnable()
        {
            YARG.Song.SongContainer.SongsRefreshed += OnSongContainerRefreshed;

            if (isClient)
            {
                EnsureSongLibraryUpload(restart: false);
            }
        }

        private void OnDisable()
        {
            YARG.Song.SongContainer.SongsRefreshed -= OnSongContainerRefreshed;

            if (_songLibraryUploadRoutine != null)
            {
                StopCoroutine(_songLibraryUploadRoutine);
                _songLibraryUploadRoutine = null;
            }

            _lastUploadedSongVersion = -1;
        }

        private void OnDestroy()
        {
            YARG.Song.SongContainer.SongsRefreshed -= OnSongContainerRefreshed;
        }

        [SyncVar]
        private int playerIndex = 0;

        [SyncVar]
        private bool isHost = false;

        [SyncVar]
        private float ping = 0f;

        [SyncVar(hook = nameof(OnReadyStateChanged))]
        private bool isReady = false;

        [Header("Instrument & Difficulty")]
        [SyncVar(hook = nameof(OnInstrumentChanged))]
        private int instrument = 0; // Use int for network sync, map to enum

        [SyncVar(hook = nameof(OnDifficultyChanged))]
        private int difficulty = 0;

        [Header("Game State")]
        [SyncVar]
        private int currentScore = 0;

        [SyncVar]
        private int currentCombo = 0;

        [SyncVar]
        private int currentStreak = 0;

        [SyncVar]
        private bool isStarPowerActive = false;

        [SyncVar]
        private float starPowerAmount = 0f;

        [SyncVar]
        private int starPowerPhrasesHit = 0;

        [SyncVar]
        private int totalStarPowerPhrases = 0;

        [SyncVar]
        private int notesHit = 0;

        [SyncVar]
        private int notesMissed = 0;

        [SyncVar]
        private int bandBonusScore = 0;

        [SyncVar]
        private int overstrums = 0;

        [SyncVar]
        private int hoposStrummed = 0;

        [SyncVar]
        private int overhits = 0;

        [SyncVar]
        private int ghostInputs = 0;

        [SyncVar]
        private int ghostsHit = 0;

        [SyncVar]
        private int accentsHit = 0;

        [SyncVar]
        private int dynamicsBonus = 0;

        [SyncVar]
        private int vocalsTicksHit = 0;

        [SyncVar]
        private int vocalsTicksMissed = 0;

        [SyncVar]
        private float vocalsPhraseTicksHit = 0f;

        [SyncVar]
        private int vocalsPhraseTicksTotal = 0;

        [SyncVar]
        private uint lastGameplaySnapshotSequence = 0;

        [SyncVar]
        private bool soloActive = false;

        [SyncVar]
        private int soloSequence = -1;

        [SyncVar]
        private int soloNoteCount = 0;

        [SyncVar]
        private int soloNotesHit = 0;

        [SyncVar]
        private int soloLastBonus = 0;

        [SyncVar]
        private int soloTotalBonus = 0;

        [SyncVar]
        private double lastGameplaySongTime = 0d;

        [SyncVar]
        private double lastGameplayNetworkTime = 0d;

        [SyncVar]
        private float lastGameplayLatencyMs = 0f;

        [SyncVar(hook = nameof(OnGameplayReadyChanged))]
        private bool gameplayReady = false;

        [SyncVar]
        private double gameplayReadyServerTime = 0d;

        [SyncVar]
        private bool hasFailed = false;
        
        private double _lastStaleSnapshotLogTime = double.MinValue;
        private bool _localAuthorityInitialized;
        private Coroutine _songLibraryUploadRoutine;
        private int _lastUploadedSongVersion = -1;

        // Events
        public event Action<string> OnPlayerNameChangedEvent;
        public event Action<bool> OnReadyStateChangedEvent;
        public event Action<int, int> OnInstrumentChangedEvent; // (instrument, difficulty)
        public event Action<int, int> OnDifficultyChangedEvent; // (instrument, difficulty)
        // Properties
        public string PlayerName => playerName;
        public int PlayerIndex => playerIndex;
        public bool IsHost => isHost;
        public float Ping => ping;
        public bool IsReady => isReady;
        public int Instrument => instrument;
        public int Difficulty => difficulty;
        public int CurrentScore => currentScore;
        public int CurrentCombo => currentCombo;
        public int CurrentStreak => currentStreak;
        public bool IsStarPowerActive => isStarPowerActive;
        public float StarPowerAmount => starPowerAmount;
        public int StarPowerPhrasesHit => starPowerPhrasesHit;
        public int TotalStarPowerPhrases => totalStarPowerPhrases;
        public int NotesHit => notesHit;
        public int NotesMissed => notesMissed;
        public int BandBonusScore => bandBonusScore;
        public int Overstrums => overstrums;
        public int HoposStrummed => hoposStrummed;
        public int Overhits => overhits;
        public int GhostInputs => ghostInputs;
        public int GhostsHit => ghostsHit;
        public int AccentsHit => accentsHit;
        public int DynamicsBonus => dynamicsBonus;
        public int VocalsTicksHit => vocalsTicksHit;
        public int VocalsTicksMissed => vocalsTicksMissed;
        public float VocalsPhraseTicksHit => vocalsPhraseTicksHit;
        public int VocalsPhraseTicksTotal => vocalsPhraseTicksTotal;
        public uint LastGameplaySnapshotSequence => lastGameplaySnapshotSequence;
        public double LastGameplaySongTime => lastGameplaySongTime;
        public double LastGameplayNetworkTime => lastGameplayNetworkTime;
        public float LastGameplayLatencyMs => lastGameplayLatencyMs;
        public bool SoloActive => soloActive;
        public int SoloSequence => soloSequence;
        public int SoloNoteCount => soloNoteCount;
        public int SoloNotesHit => soloNotesHit;
        public int SoloLastBonus => soloLastBonus;
        public int SoloTotalBonus => soloTotalBonus;
        public bool GameplayReady => gameplayReady;
        public double GameplayReadyServerTime => gameplayReadyServerTime;
        public bool HasFailed => hasFailed;
        public bool IsLocalUser
        {
            get
            {
                if (isClient && !NetworkServer.active)
                {
                    return isLocalPlayer || isOwned;
                }

                if (isClient && NetworkServer.active)
                {
                    if (isLocalPlayer)
                    {
                        return true;
                    }
                }

                if (NetworkServer.active && NetworkServer.localConnection != null)
                {
                    return connectionToClient != null && connectionToClient == NetworkServer.localConnection;
                }

                return false;
            }
        }
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            InitializeLocalAuthority();
            EnsureSongLibraryUpload(restart: false);
        }

        public override void OnStartAuthority()
        {
            base.OnStartAuthority();
            InitializeLocalAuthority();
            EnsureSongLibraryUpload(restart: false);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (isClient && (isOwned || isLocalPlayer))
            {
                EnsureSongLibraryUpload(restart: false);
            }
        }

        private void InitializeLocalAuthority()
        {
            if (_localAuthorityInitialized || !isClient)
            {
                return;
            }

            _localAuthorityInitialized = true;

            string profileName = YargNetworkManager.Instance != null
                ? YargNetworkManager.Instance.GetPlayerNameFromProfile(playerIndex)
                : playerName;
            if (!string.IsNullOrWhiteSpace(profileName))
            {
                CmdSetPlayerName(profileName);
            }

            if (isActiveAndEnabled)
            {
                StartCoroutine(MeasurePing());
            }

            YargNetworkManager.Instance?.OnLocalNetworkPlayerReady(this);

            if (isActiveAndEnabled)
            {
                EnsureSongLibraryUpload(restart: false);
            }
        }

        private void OnSongContainerRefreshed()
        {
            if (!_localAuthorityInitialized)
            {
                return;
            }

            EnsureSongLibraryUpload(restart: true);
        }

        private void EnsureSongLibraryUpload(bool restart)
        {
            if (!isClient || (!IsLocalUser && !isOwned) || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                return;
            }

            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive || !NetworkClient.active)
            {
                return;
            }

            int currentVersion = YARG.Song.SongContainer.RefreshVersion;

            if (!restart)
            {
                if (_lastUploadedSongVersion == currentVersion)
                {
                    return;
                }

                if (_songLibraryUploadRoutine != null)
                {
                    return;
                }
            }
            else if (_lastUploadedSongVersion == currentVersion && _songLibraryUploadRoutine == null)
            {
                return;
            }

            if (_songLibraryUploadRoutine != null)
            {
                StopCoroutine(_songLibraryUploadRoutine);
                _songLibraryUploadRoutine = null;
            }

            _songLibraryUploadRoutine = StartCoroutine(UploadSongLibrary());
        }

        private IEnumerator MeasurePing()
        {
            while (IsLocalUser)
            {
                // Wait 1 second between ping updates
                yield return new WaitForSeconds(1f);
                
                // Calculate ping using Mirror's NetworkTime
                // RTT (Round Trip Time) is the ping
                float rtt = (float)(Mirror.NetworkTime.rtt * 1000.0); // Convert to milliseconds
                
                // Send ping to server to sync with all clients
                CmdUpdatePing(rtt);
            }
        }

        private IEnumerator UploadSongLibrary()
        {
            const float MAX_WAIT_SECONDS = 10f;
            float waited = 0f;

            try
            {
                while (YARG.Song.SongContainer.Count == 0 && waited < MAX_WAIT_SECONDS)
                {
                    yield return null;
                    waited += Time.unscaledDeltaTime;
                }

                if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive || !NetworkClient.active)
                {
                    yield break;
                }

                int refreshVersion = YARG.Song.SongContainer.RefreshVersion;
                var hashList = YARG.Song.SongContainer.SongHashes;
                int totalSongs = hashList.Count;

                bool isFirstChunk = true;
                if (totalSongs == 0)
                {
                    CmdSubmitSongLibraryChunk(Array.Empty<byte>(), true, true);
                    _lastUploadedSongVersion = refreshVersion;
                    yield break;
                }

                int hashSize = HashWrapper.HASH_SIZE_IN_BYTES;
                int index = 0;

                while (index < totalSongs)
                {
                    int chunkSongCount = Math.Min(SONG_HASHES_PER_CHUNK, totalSongs - index);
                    using var stream = new MemoryStream(chunkSongCount * hashSize);

                    for (int i = 0; i < chunkSongCount; i++)
                    {
                        hashList[index + i].Serialize(stream);
                    }

                    byte[] chunk = stream.ToArray();
                    bool isFinalChunk = (index + chunkSongCount) >= totalSongs;
                    CmdSubmitSongLibraryChunk(chunk, isFirstChunk, isFinalChunk);

                    isFirstChunk = false;
                    index += chunkSongCount;

                    // Yield occasionally to avoid long stalls for extremely large libraries
                    if (index < totalSongs)
                    {
                        yield return null;
                    }
                }

                _lastUploadedSongVersion = refreshVersion;
            }
            finally
            {
                _songLibraryUploadRoutine = null;
            }
        }

        [Command]
        private void CmdSubmitSongLibraryChunk(byte[] chunk, bool isFirstChunk, bool isFinalChunk)
        {
            if (YargNetworkManager.Instance == null)
            {
                return;
            }

            YargNetworkManager.Instance.ServerRegisterSongLibraryChunk(this, chunk ?? Array.Empty<byte>(), isFirstChunk, isFinalChunk);
        }

        /// <summary>
        /// Sync lobby info from server to this client.
        /// Called by server after spawning the player.
        /// </summary>
        [TargetRpc]
        public void TargetSyncLobbyInfo(string lobbyName, string hostName, int maxPlayers, bool hasPassword, int privacyMode)
        {
            Debug.Log($"[NetworkPlayerData] TargetSyncLobbyInfo CALLED! Lobby: {lobbyName}, Host: {hostName}, MaxPlayers: {maxPlayers}");
            
            if (YargNetworkManager.Instance == null)
            {
                Debug.LogError("[NetworkPlayerData] YargNetworkManager.Instance is NULL!");
                return;
            }
            
            if (YargNetworkManager.Instance.CurrentLobby == null)
            {
                Debug.LogError("[NetworkPlayerData] CurrentLobby is NULL!");
                return;
            }
            
            var lobby = YargNetworkManager.Instance.CurrentLobby;
            Debug.Log($"[NetworkPlayerData] Before update - Lobby name: {lobby.lobbyName}");
            
            lobby.lobbyName = lobbyName;
            lobby.hostName = hostName;
            lobby.maxPlayers = maxPlayers;
            lobby.hasPassword = hasPassword;
            lobby.privacyMode = (YargNetworkManager.LobbyPrivacyMode)privacyMode;
            lobby.currentPlayers = NetworkServer.active ? NetworkServer.connections.Count : 2;
            
            Debug.Log($"[NetworkPlayerData] After update - Lobby name: {lobby.lobbyName}");
            Debug.Log($"[NetworkPlayerData] Triggering TriggerLobbyJoinedEvent...");
            
            // Trigger update event so UI refreshes
            YargNetworkManager.Instance.TriggerLobbyJoinedEvent(lobby);
            
            Debug.Log("[NetworkPlayerData] TriggerLobbyJoinedEvent completed");
        }

        /// <summary>
        /// Tell this specific client to navigate to the music library.
        /// Called by server when host starts song selection.
        /// </summary>
        [TargetRpc]
        public void TargetNavigateToMusicLibrary()
        {
            Debug.Log("[NetworkPlayerData] TargetNavigateToMusicLibrary received - navigating to music library");
            
            // Navigate to music library only if not already there
            // (Host already pushed the menu locally, clients need to navigate)
            var menuManager = FindObjectOfType<YARG.Menu.MenuManager>();
            if (menuManager != null)
            {
                // Check if we're already on MusicLibrary to avoid duplicate pushes
                if (menuManager.CurrentMenu != YARG.Menu.MenuManager.Menu.MusicLibrary)
                {
                    Debug.Log("[NetworkPlayerData] Pushing MusicLibrary menu");
                    menuManager.PushMenu(YARG.Menu.MenuManager.Menu.MusicLibrary);
                }
                else
                {
                    Debug.Log("[NetworkPlayerData] Already on MusicLibrary, skipping push");
                }
            }
            else
            {
                Debug.LogWarning("[NetworkPlayerData] MenuManager not found");
            }
        }

        /// <summary>
        /// Tell this specific client that a song was selected.
        /// Called by server when host selects a song in music library.
        /// </summary>
        [TargetRpc]
        public void TargetSongSelected(string songHash)
        {
            Debug.Log($"[NetworkPlayerData] TargetSongSelected received - songHash: {songHash}");
            
            // Find the song in the song container
            var songsByHash = YARG.Song.SongContainer.SongsByHash;
            var hashWrapper = YARG.Core.Song.HashWrapper.FromString(songHash);
            
            if (songsByHash != null && songsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
            {
                var song = songList[0];
                Debug.Log($"[NetworkPlayerData] Found song: {song.Name} by {song.Artist}");
                
                // Trigger the OnSongSelected event in network manager
                if (YargNetworkManager.Instance != null)
                {
                    YargNetworkManager.Instance.TriggerSongSelectedEvent(song);
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkPlayerData] Song not found in container: {songHash}");
            }
        }

        [TargetRpc]
        public void TargetReceiveSharedSongChunk(byte[] chunk, bool isFirstChunk, bool isFinalChunk)
        {
            if (isFirstChunk)
            {
                MultiplayerSongFilter.BeginSharedSongsUpload();
            }

            if (chunk != null && chunk.Length > 0)
            {
                MultiplayerSongFilter.AppendSharedSongsChunk(chunk);
            }

            if (isFinalChunk)
            {
                MultiplayerSongFilter.CommitSharedSongsUpload();
            }
        }

        [TargetRpc]
        public void TargetClearSharedSongs()
        {
            MultiplayerSongFilter.ClearSharedSongs();
        }

        /// <summary>
        /// Tell this specific client to start the multiplayer song.
        /// Sets CurrentSong and navigates to difficulty select.
        /// </summary>
        [TargetRpc]
        public void TargetStartMultiplayerSong(string songHash)
        {
            Debug.Log($"[NetworkPlayerData] TargetStartMultiplayerSong received - songHash: {songHash}");
            
            // Find the song in the song container
            var songsByHash = YARG.Song.SongContainer.SongsByHash;
            var hashWrapper = YARG.Core.Song.HashWrapper.FromString(songHash);
            
            if (songsByHash != null && songsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
            {
                var song = songList[0];
                Debug.Log($"[NetworkPlayerData] Starting song: {song.Name} by {song.Artist}");
                
                // Set global state
                GlobalVariables.State.CurrentSong = song;
                GlobalVariables.State.ShowSongs.Clear();
                GlobalVariables.State.ShowSongs.Add(song);
                GlobalVariables.State.PlayingAShow = false;
                
                // Navigate to difficulty select only if not already there
                // (Host already pushed the menu locally, clients need to navigate)
                var menuManager = FindObjectOfType<YARG.Menu.MenuManager>();
                if (menuManager != null)
                {
                    // Check if we're already on DifficultySelect to avoid duplicate pushes
                    if (menuManager.CurrentMenu != YARG.Menu.MenuManager.Menu.DifficultySelect)
                    {
                        Debug.Log("[NetworkPlayerData] Pushing DifficultySelect menu");
                        menuManager.PushMenu(YARG.Menu.MenuManager.Menu.DifficultySelect);
                    }
                    else
                    {
                        Debug.Log("[NetworkPlayerData] Already on DifficultySelect, skipping push");
                    }
                }
                else
                {
                    Debug.LogWarning("[NetworkPlayerData] MenuManager not found");
                }
            }
            else
            {
                Debug.LogWarning($"[NetworkPlayerData] Song not found in container: {songHash}");
            }
        }

        #region Commands (Client -> Server)

        /// <summary>
        /// Set player name directly on the server (used during spawn).
        /// </summary>
        [Server]
        public void SetPlayerNameServer(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogWarning($"[NetworkPlayerData] Player name cannot be empty for {netId}");
                return;
            }

            // Limit to 32 characters (Steam profile name limit)
            if (name.Length > YargNetworkManager.MAX_PLAYER_NAME_LENGTH)
            {
                name = name.Substring(0, YargNetworkManager.MAX_PLAYER_NAME_LENGTH);
                Debug.LogWarning($"[NetworkPlayerData] Player name truncated to {YargNetworkManager.MAX_PLAYER_NAME_LENGTH} characters.");
            }

            playerName = name;
        }

        [Server]
        public void SetGameplayReadyServer(bool ready)
        {
            bool previous = gameplayReady;
            gameplayReady = ready;
            gameplayReadyServerTime = ready ? Mirror.NetworkTime.time : 0d;

            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ServerOnPlayerGameplayReadyStateChanged(this, previous, ready);
            }
        }

        [Server]
        public void SetPlayerIndexServer(int index)
        {
            playerIndex = index;
        }

        [Server]
        public void SetInstrumentServer(int instrumentType, int difficultyLevel)
        {
            instrument = instrumentType;
            difficulty = difficultyLevel;
        }

        /// <summary>
        /// Set player name.
        /// </summary>
        [Command]
        public void CmdSetPlayerName(string newName)
        {
            if (string.IsNullOrEmpty(newName))
            {
                Debug.LogWarning($"[NetworkPlayerData] Player name cannot be empty for {netId}");
                return;
            }

            // Limit to 32 characters (Steam profile name limit)
            if (newName.Length > YargNetworkManager.MAX_PLAYER_NAME_LENGTH)
            {
                newName = newName.Substring(0, YargNetworkManager.MAX_PLAYER_NAME_LENGTH);
                Debug.LogWarning($"[NetworkPlayerData] Player name truncated to {YargNetworkManager.MAX_PLAYER_NAME_LENGTH} characters.");
            }

            playerName = newName;
        }

        [Command]
        public void CmdUpdatePing(float pingValue)
        {
            ping = pingValue;
        }

        [Command]
        public void CmdSetGameplayReady(bool ready)
        {
            SetGameplayReadyServer(ready);
        }

        [Command]
        public void CmdReportSongFailed()
        {
            if (!NetworkServer.active)
            {
                return;
            }

            ServerRegisterFailure();
        }

        [Server]
        internal void ServerRegisterFailure()
        {
            if (hasFailed)
            {
                return;
            }

            hasFailed = true;

            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ServerOnPlayerFailed(this);
            }
        }

        [Server]
        internal void ServerClearFailureFlag()
        {
            hasFailed = false;
        }

        [TargetRpc]
        internal void TargetHandleBandFailed(NetworkConnection target)
        {
            ClientHandleBandFailed();
        }

        [Client]
        internal void ClientHandleBandFailed()
        {
            UnityMainThreadCallback.QueueEvent(() =>
            {
                var manager = UnityEngine.Object.FindObjectOfType<GameManager>();
                manager?.HandleNetworkBandFailed();
            });
        }

        /// <summary>
        /// Server-side method to set host flag (called from YargNetworkManager).
        /// </summary>
        public void SetIsHostServer(bool value)
        {
            if (!NetworkServer.active) return;
            isHost = value;
        }

        /// <summary>
        /// Set player index (for local multiplayer).
        /// </summary>
        [Command]
        public void CmdSetPlayerIndex(int index)
        {
            playerIndex = index;
        }

        /// <summary>
        /// Set ready state.
        /// </summary>
        [Command]
        public void CmdSetReady(bool ready)
        {
            isReady = ready;
        }

        [Server]
        public void SetReadyStateServer(bool ready)
        {
            isReady = ready;
        }

        /// <summary>
        /// Set instrument and difficulty.
        /// </summary>
        [Command]
        public void CmdSetInstrument(int instrumentType, int difficultyLevel)
        {
            instrument = instrumentType;
            difficulty = difficultyLevel;
        }

        [Command]
        public void CmdSyncLocalPlayerSlots(string[] playerNames, int[] instruments, int[] difficulties)
        {
            if (!NetworkServer.active || YargNetworkManager.Instance == null)
            {
                return;
            }

            YargNetworkManager.Instance.ServerSyncLocalPlayerSlots(connectionToClient, playerNames, instruments, difficulties);
        }

        [Command]
        public void CmdSubmitGameplaySnapshot(int score, int combo, int streak, bool starPowerActiveState,
            float starPowerCharge, int authoritativeStarPowerPhrasesHit, int authoritativeTotalStarPowerPhrases,
            int authoritativeNotesHit, int authoritativeNotesMissed,
            int authoritativeOverstrums, int authoritativeHoposStrummed, int authoritativeOverhits,
            int authoritativeGhostInputs, int authoritativeGhostsHit, int authoritativeAccentsHit,
            int authoritativeDynamicsBonus, int authoritativeBandBonusScore, int authoritativeVocalsTicksHit,
            int authoritativeVocalsTicksMissed, float authoritativeVocalsPhraseTicksHit,
            int authoritativeVocalsPhraseTicksTotal, bool authoritativeSoloActive, int authoritativeSoloSequence,
            int authoritativeSoloNoteCount, int authoritativeSoloNotesHit, int authoritativeSoloLastBonus,
            int authoritativeSoloTotalBonus, double clientSongTime, double clientNetworkTime, uint sequence)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning($"[NetworkPlayerData] Received CmdSubmitGameplaySnapshot while server inactive for {playerName}");
                return;
            }

            if (sequence <= lastGameplaySnapshotSequence)
            {
                double serverTime = Mirror.NetworkTime.time;
                if (sequence < lastGameplaySnapshotSequence &&
                    serverTime - _lastStaleSnapshotLogTime > SNAPSHOT_OUT_OF_ORDER_LOG_COOLDOWN)
                {
                    Debug.LogWarning($"[NetworkPlayerData] Stale snapshot from {playerName}: sequence {sequence} < {lastGameplaySnapshotSequence}");
                    _lastStaleSnapshotLogTime = serverTime;
                }
                return;
            }

            if (score < 0 || combo < 0 || streak < 0 ||
                authoritativeStarPowerPhrasesHit < 0 || authoritativeTotalStarPowerPhrases < 0 ||
                authoritativeNotesHit < 0 || authoritativeNotesMissed < 0 ||
                authoritativeOverstrums < 0 || authoritativeHoposStrummed < 0 ||
                authoritativeOverhits < 0 || authoritativeGhostInputs < 0 ||
                authoritativeGhostsHit < 0 || authoritativeAccentsHit < 0 ||
                authoritativeDynamicsBonus < 0 || authoritativeBandBonusScore < 0 ||
                authoritativeVocalsTicksHit < 0 || authoritativeVocalsTicksMissed < 0 ||
                authoritativeVocalsPhraseTicksHit < 0f || authoritativeVocalsPhraseTicksTotal < 0)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName} due to negative values.");
                return;
            }

            if (authoritativeStarPowerPhrasesHit > authoritativeTotalStarPowerPhrases)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: star power phrases hit exceeds total ({authoritativeStarPowerPhrasesHit} > {authoritativeTotalStarPowerPhrases}).");
                return;
            }

            if (authoritativeSoloSequence < -1)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: soloSequence invalid ({authoritativeSoloSequence}).");
                return;
            }

            if (authoritativeSoloNoteCount < 0 || authoritativeSoloNotesHit < 0 || authoritativeSoloLastBonus < 0 ||
                authoritativeSoloTotalBonus < 0)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: negative solo values.");
                return;
            }

            if (authoritativeSoloNoteCount < authoritativeSoloNotesHit)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: soloNotesHit exceeds note count ({authoritativeSoloNotesHit} > {authoritativeSoloNoteCount}).");
                return;
            }

            float clampedStarPower = Mathf.Clamp01(starPowerCharge);

            if (score < currentScore)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting non-monotonic score snapshot from {playerName}: {score} < {currentScore}");
                return;
            }

            if (authoritativeNotesHit < notesHit)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: notesHit regressed ({authoritativeNotesHit} < {notesHit}).");
                return;
            }

            if (authoritativeNotesMissed < notesMissed)
            {
                LogStateRegression("notesMissed", notesMissed, authoritativeNotesMissed);
            }

            if (authoritativeOverstrums < overstrums)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: overstrums regressed ({authoritativeOverstrums} < {overstrums}).");
                return;
            }

            if (authoritativeHoposStrummed < hoposStrummed)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: hoposStrummed regressed ({authoritativeHoposStrummed} < {hoposStrummed}).");
                return;
            }

            if (authoritativeOverhits < overhits)
            {
                LogStateRegression("overhits", overhits, authoritativeOverhits);
            }

            if (authoritativeGhostInputs < ghostInputs)
            {
                LogStateRegression("ghostInputs", ghostInputs, authoritativeGhostInputs);
            }

            if (authoritativeGhostsHit < ghostsHit)
            {
                LogStateRegression("ghostsHit", ghostsHit, authoritativeGhostsHit);
            }

            if (authoritativeAccentsHit < accentsHit)
            {
                LogStateRegression("accentsHit", accentsHit, authoritativeAccentsHit);
            }

            if (authoritativeDynamicsBonus < dynamicsBonus)
            {
                LogStateRegression("dynamicsBonus", dynamicsBonus, authoritativeDynamicsBonus);
            }

            if (authoritativeBandBonusScore < bandBonusScore)
            {
                LogStateRegression("bandBonusScore", bandBonusScore, authoritativeBandBonusScore);
            }

            if (authoritativeVocalsTicksHit < vocalsTicksHit)
            {
                LogStateRegression("vocalsTicksHit", vocalsTicksHit, authoritativeVocalsTicksHit);
            }

            if (authoritativeVocalsTicksMissed < vocalsTicksMissed)
            {
                LogStateRegression("vocalsTicksMissed", vocalsTicksMissed, authoritativeVocalsTicksMissed);
            }

            if (authoritativeSoloTotalBonus < soloTotalBonus)
            {
                LogStateRegression("soloTotalBonus", soloTotalBonus, authoritativeSoloTotalBonus);
            }

            if (authoritativeStarPowerPhrasesHit < starPowerPhrasesHit)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: star power phrases hit regressed ({authoritativeStarPowerPhrasesHit} < {starPowerPhrasesHit}).");
                return;
            }

            if (authoritativeTotalStarPowerPhrases < totalStarPowerPhrases)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: total star power phrases regressed ({authoritativeTotalStarPowerPhrases} < {totalStarPowerPhrases}).");
                return;
            }

            if (authoritativeSoloSequence < soloSequence)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: soloSequence regressed ({authoritativeSoloSequence} < {soloSequence}).");
                return;
            }

            if (authoritativeSoloSequence == soloSequence && authoritativeSoloNotesHit < soloNotesHit)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: soloNotesHit regressed within sequence ({authoritativeSoloNotesHit} < {soloNotesHit}).");
                return;
            }
            if (authoritativeVocalsPhraseTicksTotal == 0)
            {
                authoritativeVocalsPhraseTicksHit = 0f;
            }
            else if (authoritativeVocalsPhraseTicksHit > authoritativeVocalsPhraseTicksTotal)
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: vocalsPhraseTicksHit exceeds total ({authoritativeVocalsPhraseTicksHit} > {authoritativeVocalsPhraseTicksTotal}).");
                return;
            }

            if (float.IsNaN(authoritativeVocalsPhraseTicksHit) || float.IsInfinity(authoritativeVocalsPhraseTicksHit))
            {
                Debug.LogWarning($"[NetworkPlayerData] Rejecting snapshot from {playerName}: vocalsPhraseTicksHit is invalid ({authoritativeVocalsPhraseTicksHit}).");
                return;
            }

            int scoreDelta = score - currentScore;
            int notesDelta = authoritativeNotesHit - notesHit;

            if (scoreDelta > SCORE_DELTA_WARNING)
            {
                Debug.LogWarning($"[NetworkPlayerData] Large score delta detected for {playerName}: +{scoreDelta}.");
            }

            if (notesDelta > NOTES_DELTA_WARNING)
            {
                Debug.LogWarning($"[NetworkPlayerData] Large notesHit delta detected for {playerName}: +{notesDelta}.");
            }

            double serverTimeNow = Mirror.NetworkTime.time;
            float latencyMs = (float)Math.Max(0.0, (serverTimeNow - clientNetworkTime) * 1000.0);
            lastGameplayLatencyMs = latencyMs;
            if (latencyMs > LATENCY_WARNING_THRESHOLD_MS)
            {
                Debug.LogWarning($"[NetworkPlayerData] Snapshot latency {latencyMs:F1}ms for {playerName} exceeds threshold.");
            }

            currentScore = score;
            currentCombo = combo;
            currentStreak = streak;
            isStarPowerActive = starPowerActiveState;
            starPowerAmount = clampedStarPower;
            starPowerPhrasesHit = authoritativeStarPowerPhrasesHit;
            totalStarPowerPhrases = authoritativeTotalStarPowerPhrases;
            notesHit = authoritativeNotesHit;
            notesMissed = authoritativeNotesMissed;
            bandBonusScore = authoritativeBandBonusScore;
            overstrums = authoritativeOverstrums;
            hoposStrummed = authoritativeHoposStrummed;
            overhits = authoritativeOverhits;
            ghostInputs = authoritativeGhostInputs;
            ghostsHit = authoritativeGhostsHit;
            accentsHit = authoritativeAccentsHit;
            dynamicsBonus = authoritativeDynamicsBonus;
            vocalsTicksHit = authoritativeVocalsTicksHit;
            vocalsTicksMissed = authoritativeVocalsTicksMissed;
            if (authoritativeVocalsPhraseTicksTotal > 0)
            {
                vocalsPhraseTicksTotal = authoritativeVocalsPhraseTicksTotal;
                vocalsPhraseTicksHit = Mathf.Clamp(authoritativeVocalsPhraseTicksHit, 0f, authoritativeVocalsPhraseTicksTotal);
            }
            else
            {
                vocalsPhraseTicksTotal = 0;
                vocalsPhraseTicksHit = 0f;
            }
            lastGameplaySnapshotSequence = sequence;
            lastGameplaySongTime = clientSongTime;
            lastGameplayNetworkTime = clientNetworkTime;
            soloActive = authoritativeSoloActive;
            soloSequence = authoritativeSoloSequence;
            soloNoteCount = authoritativeSoloNoteCount;
            soloNotesHit = authoritativeSoloNotesHit;
            soloLastBonus = authoritativeSoloLastBonus;
            soloTotalBonus = authoritativeSoloTotalBonus;
        }
        
        /// <summary>
        /// Reset game state for new song.
        /// </summary>
        [Command]
        public void CmdResetGameState()
        {
            ResetGameplaySnapshotState();
        }

        #endregion

        #region SyncVar Hooks

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            OnPlayerNameChangedEvent?.Invoke(newName);
        }

        private void OnReadyStateChanged(bool oldState, bool newState)
        {
            OnReadyStateChangedEvent?.Invoke(newState);
        }

        private void OnGameplayReadyChanged(bool oldState, bool newState)
        {
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ClientOnRemoteGameplayReadyStateChanged(this, newState);
            }
        }
        
        private void OnInstrumentChanged(int oldInstrument, int newInstrument)
        {
            OnInstrumentChangedEvent?.Invoke(newInstrument, difficulty);
        }
        
        private void OnDifficultyChanged(int oldDifficulty, int newDifficulty)
        {
            OnDifficultyChangedEvent?.Invoke(instrument, newDifficulty);
        }

        #endregion

        private void ResetGameplaySnapshotState()
        {
            currentScore = 0;
            currentCombo = 0;
            currentStreak = 0;
            isStarPowerActive = false;
            starPowerAmount = 0f;
            starPowerPhrasesHit = 0;
            totalStarPowerPhrases = 0;
            notesHit = 0;
            notesMissed = 0;
            bandBonusScore = 0;
            overstrums = 0;
            hoposStrummed = 0;
            overhits = 0;
            ghostInputs = 0;
            ghostsHit = 0;
            accentsHit = 0;
            dynamicsBonus = 0;
            vocalsTicksHit = 0;
            vocalsTicksMissed = 0;
            vocalsPhraseTicksHit = 0f;
            vocalsPhraseTicksTotal = 0;
            soloActive = false;
            soloSequence = -1;
            soloNoteCount = 0;
            soloNotesHit = 0;
            soloLastBonus = 0;
            soloTotalBonus = 0;
            lastGameplaySnapshotSequence = 0;
            lastGameplaySongTime = 0d;
            lastGameplayNetworkTime = 0d;
            lastGameplayLatencyMs = 0f;
            _lastStaleSnapshotLogTime = double.MinValue;
            hasFailed = false;
        }

        private void LogStateRegression(string statName, int previousValue, int newValue)
        {
            int regression = previousValue - newValue;
            if (regression <= 0)
            {
                return;
            }

            if (regression > NOTES_DELTA_WARNING)
            {
                Debug.LogWarning($"[NetworkPlayerData] {statName} regressed by {regression} for {playerName}. Accepting snapshot to resync state.");
            }
        }

        /// <summary>
        /// Sync player's profile data (instrument, difficulty, game mode).
        /// Called when player enters difficulty select.
        /// </summary>
        [Command]
        public void CmdSyncPlayerProfile(int gameMode, int instrumentType, int difficultyLevel)
        {
            // Store the data
            instrument = instrumentType;
            difficulty = difficultyLevel;
            
            Debug.Log($"[NetworkPlayerData] Player {playerName} synced profile - GameMode: {gameMode}, Instrument: {instrumentType}, Difficulty: {difficultyLevel}");
        }

        /// <summary>
        /// Tell this client to navigate to gameplay scene.
        /// Called by server when all players are ready.
        /// </summary>
        [TargetRpc]
        public void TargetStartGameplay()
        {
            Debug.Log($"[NetworkPlayerData] TargetStartGameplay received - CurrentScene: {GlobalVariables.Instance?.CurrentScene}");
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ClientPrepareGameplayStartBarrier();
            }
            
            // Skip if already transitioning to Gameplay scene
            if (GlobalVariables.Instance != null && GlobalVariables.Instance.CurrentScene == SceneIndex.Gameplay)
            {
                Debug.Log("[NetworkPlayerData] Already in Gameplay scene, skipping scene load");
                return;
            }
            
            Debug.Log("[NetworkPlayerData] Starting scene transition to Gameplay");
            
            // Use GlobalVariables.LoadScene for proper additive scene loading
            // This maintains the Persistent scene with network objects
            if (GlobalVariables.Instance != null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            }
            else
            {
                // Fallback: Load scene directly if GlobalVariables is null
                Debug.LogWarning("[NetworkPlayerData] GlobalVariables.Instance is null! Loading scene directly.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
            }
        }

        [TargetRpc]
        internal void TargetConfirmGameplayStart(double serverTime, float startDelaySeconds)
        {
            Debug.Log($"[NetworkPlayerData] TargetConfirmGameplayStart received - serverTime: {serverTime:F4}, delay: {startDelaySeconds:F3}s");

            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ClientHandleGameplayStartSignal(serverTime, startDelaySeconds);
            }
        }

        [TargetRpc]
        public void TargetBeginNextShowSong(string songHash, int showIndex)
        {
            Debug.Log($"[NetworkPlayerData] TargetBeginNextShowSong received - showIndex: {showIndex}, hash: {songHash}");

            GlobalVariables.State.PlayingAShow = true;
            GlobalVariables.State.ShowIndex = showIndex;

            var showSongs = GlobalVariables.State.ShowSongs;
            if (showSongs != null && showIndex >= 0 && showIndex < showSongs.Count)
            {
                GlobalVariables.State.CurrentSong = showSongs[showIndex];
            }
            else
            {
                var songsByHash = YARG.Song.SongContainer.SongsByHash;
                var hashWrapper = YARG.Core.Song.HashWrapper.FromString(songHash);
                if (songsByHash != null && songsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    GlobalVariables.State.CurrentSong = songList[0];
                }
                else
                {
                    Debug.LogWarning("[NetworkPlayerData] Failed to resolve next show song from hash. Returning to music library.");
                    TargetReturnToMusicLibraryAfterScore();
                    return;
                }
            }

            if (GlobalVariables.State.CurrentSong == null)
            {
                Debug.LogWarning("[NetworkPlayerData] CurrentSong is null after resolving show song. Returning to music library.");
                TargetReturnToMusicLibraryAfterScore();
                return;
            }

            YargNetworkManager.SetMenuNavigationAfterSceneLoad(
                Menu.MenuManager.Menu.OnlineMultiplayer,
                Menu.MenuManager.Menu.LobbyRoom,
                Menu.MenuManager.Menu.DifficultySelect);

            if (GlobalVariables.Instance != null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayerData] GlobalVariables.Instance is null! Loading Menu scene directly.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }

        [TargetRpc]
        public void TargetReturnToMusicLibraryAfterScore()
        {
            Debug.Log("[NetworkPlayerData] TargetReturnToMusicLibraryAfterScore received");

            GlobalVariables.State.PlayingAShow = false;
            GlobalVariables.State.ShowIndex = 0;

            YargNetworkManager.SetMenuNavigationAfterSceneLoad(
                Menu.MenuManager.Menu.OnlineMultiplayer,
                Menu.MenuManager.Menu.LobbyRoom,
                Menu.MenuManager.Menu.MusicLibrary);

            if (GlobalVariables.Instance != null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayerData] GlobalVariables.Instance is null! Loading Menu scene directly.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }
        
        /// <summary>
        /// Tell this client to restart gameplay (reload Gameplay scene).
        /// Called by server when host restarts from pause menu.
        /// </summary>
        [TargetRpc]
        public void TargetRestartGameplay()
        {
            Debug.Log("[NetworkPlayerData] TargetRestartGameplay received - reloading Gameplay scene");
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.ClientPrepareGameplayStartBarrier();
            }
            
            if (GlobalVariables.Instance != null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Gameplay);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayerData] GlobalVariables.Instance is null! Loading scene directly.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Gameplay");
            }
        }
        
        /// <summary>
        /// Tell this client to sync practice mode state.
        /// Called by server when host toggles practice mode from pause menu.
        /// </summary>
        [TargetRpc]
        public void TargetSyncPracticeMode(bool isPractice)
        {
            Debug.Log($"[NetworkPlayerData] TargetSyncPracticeMode received: {isPractice}");
            GlobalVariables.State.IsPractice = isPractice;
        }
        
        /// <summary>
        /// Tell this client to quit gameplay and return to menu scene.
        /// Called by server when host quits from pause menu.
        /// </summary>
        [TargetRpc]
        public void TargetQuitGameplay()
        {
            Debug.Log("[NetworkPlayerData] TargetQuitGameplay received - loading Menu scene");
            
            // Ensure we rebuild the multiplayer navigation stack after returning to the menu scene.
            YargNetworkManager.SetMenuNavigationAfterSceneLoad(
                Menu.MenuManager.Menu.OnlineMultiplayer,
                Menu.MenuManager.Menu.LobbyRoom,
                Menu.MenuManager.Menu.MusicLibrary);

            if (GlobalVariables.Instance != null)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }
            else
            {
                Debug.LogWarning("[NetworkPlayerData] GlobalVariables.Instance is null! Loading scene directly.");
                UnityEngine.SceneManagement.SceneManager.LoadScene("Menu");
            }
        }

        /// <summary>
        /// RPC sent to all clients to navigate their menu.
        /// Called by server when host navigates (e.g., going back from difficulty select).
        /// </summary>
        [ClientRpc]
        public void RpcNavigateMenu(bool popMenu, int targetMenuInt)
        {
            // Host handles their own navigation locally, skip RPC processing
            if (NetworkServer.active) return;
            
            var targetMenu = (Menu.MenuManager.Menu)targetMenuInt;
            string action = popMenu ? "PopMenu" : $"PushMenu({targetMenu})";
            Debug.Log($"[NetworkPlayerData] Client received menu navigation command: {action}");
            
            if (Menu.MenuManager.Instance == null)
            {
                Debug.LogWarning("[NetworkPlayerData] MenuManager is null, cannot navigate");
                return;
            }
            
            // Validate client is in a multiplayer context (not at main menu)
            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive)
            {
                Debug.LogWarning("[NetworkPlayerData] Client received navigation command but not in active network session, ignoring");
                return;
            }
            
            if (popMenu)
            {
                // Only pop if there's something underneath us on the stack.
                if (Menu.MenuManager.Instance.MenuStackCount > 1)
                {
                    Menu.MenuManager.Instance.PopMenu();
                }
                else
                {
                    var currentMenu = Menu.MenuManager.Instance.CurrentMenu;
                    Debug.Log($"[NetworkPlayerData] Menu stack has a single entry ({currentMenu}), skipping pop to avoid empty stack");
                }
            }
            else if (targetMenu != Menu.MenuManager.Menu.None)
            {
                Menu.MenuManager.Instance.PushMenu(targetMenu);
            }
        }
        
        /// <summary>
        /// Show a toast notification when a player joins the lobby.
        /// </summary>
        [ClientRpc]
        public void RpcShowPlayerJoinedToast(string playerName)
        {
            Debug.Log($"[NetworkPlayerData] Showing player joined toast: {playerName}");
            Menu.Persistent.ToastManager.ToastInformation($"{playerName} joined the lobby");
        }
        
        /// <summary>
        /// Show a toast notification when a player leaves the lobby.
        /// </summary>
        [ClientRpc]
        public void RpcShowPlayerLeftToast(string playerName)
        {
            Debug.Log($"[NetworkPlayerData] Showing player left toast: {playerName}");
            Menu.Persistent.ToastManager.ToastWarning($"{playerName} left the lobby");
        }
    }
}