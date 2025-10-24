using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Song;
using YARG.Networking;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Manages the shared song queue for multiplayer sessions.
    /// Players can add songs to the queue, host can start the set.
    /// </summary>
    public class SongQueueSystem : MonoBehaviour
    {
        public static SongQueueSystem Instance { get; private set; }

        [System.Serializable]
        public class QueuedSong
        {
            public SongEntry song;
            public string queuedByPlayer;
            public System.DateTime queuedAt;

            public QueuedSong(SongEntry song, string playerName)
            {
                this.song = song;
                this.queuedByPlayer = playerName;
                this.queuedAt = System.DateTime.Now;
            }
        }

        private List<QueuedSong> _songQueue = new List<QueuedSong>();
        private int _currentSongIndex = -1;
        private bool _isPlayingSet = false;

        public List<QueuedSong> SongQueue => _songQueue;
        public int QueueCount => _songQueue.Count;
        public bool IsPlayingSet => _isPlayingSet;
        public bool HasNextSong => _currentSongIndex < _songQueue.Count - 1;
        public bool IsQueueMode => YargNetworkManager.Instance != null && 
                                   YargNetworkManager.Instance.CurrentLobby != null;

        // Events
        public event System.Action OnQueueChanged;
        public event System.Action<QueuedSong> OnSongAdded;
        public event System.Action<int> OnSongRemoved;
        public event System.Action OnSetStarted;
        public event System.Action OnSetEnded;
        public event System.Action<SongEntry> OnSongStarting;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            // Subscribe to network events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyLeft += OnLobbyLeft;
            }
        }

        private void OnDestroy()
        {
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Add a song to the queue. Can be called by any player.
        /// </summary>
        public bool AddSong(SongEntry song, string playerName = null)
        {
            if (song == null)
            {
                Debug.LogWarning("Cannot add null song to queue");
                return false;
            }

            // Get current player name if not specified
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = YargNetworkManager.Instance?.CurrentLobby?.hostName ?? "Player";
            }

            var queuedSong = new QueuedSong(song, playerName);
            _songQueue.Add(queuedSong);

            Debug.Log($"Added '{song.Name}' to queue (queued by {playerName})");

            OnSongAdded?.Invoke(queuedSong);
            OnQueueChanged?.Invoke();

            // TODO: Broadcast to all clients in multiplayer
            return true;
        }

        /// <summary>
        /// Remove a song from the queue by index. Host only.
        /// </summary>
        public bool RemoveSong(int index)
        {
            if (!IsHost())
            {
                Debug.LogWarning("Only host can remove songs from queue");
                return false;
            }

            if (index < 0 || index >= _songQueue.Count)
            {
                Debug.LogWarning($"Invalid queue index: {index}");
                return false;
            }

            if (_isPlayingSet && index <= _currentSongIndex)
            {
                Debug.LogWarning("Cannot remove currently playing or past songs");
                return false;
            }

            var removed = _songQueue[index];
            _songQueue.RemoveAt(index);

            Debug.Log($"Removed '{removed.song.Name}' from queue");

            OnSongRemoved?.Invoke(index);
            OnQueueChanged?.Invoke();

            // TODO: Broadcast to all clients in multiplayer
            return true;
        }

        /// <summary>
        /// Move a song to a different position in the queue. Host only.
        /// </summary>
        public bool ReorderSong(int fromIndex, int toIndex)
        {
            if (!IsHost())
            {
                Debug.LogWarning("Only host can reorder queue");
                return false;
            }

            if (fromIndex < 0 || fromIndex >= _songQueue.Count ||
                toIndex < 0 || toIndex >= _songQueue.Count)
            {
                Debug.LogWarning($"Invalid reorder indices: {fromIndex} -> {toIndex}");
                return false;
            }

            if (_isPlayingSet && (fromIndex <= _currentSongIndex || toIndex <= _currentSongIndex))
            {
                Debug.LogWarning("Cannot reorder currently playing or past songs");
                return false;
            }

            var song = _songQueue[fromIndex];
            _songQueue.RemoveAt(fromIndex);
            _songQueue.Insert(toIndex, song);

            Debug.Log($"Moved '{song.song.Name}' from {fromIndex} to {toIndex}");

            OnQueueChanged?.Invoke();

            // TODO: Broadcast to all clients in multiplayer
            return true;
        }

        /// <summary>
        /// Clear the entire queue. Host only.
        /// </summary>
        public bool ClearQueue()
        {
            if (!IsHost())
            {
                Debug.LogWarning("Only host can clear queue");
                return false;
            }

            if (_isPlayingSet)
            {
                Debug.LogWarning("Cannot clear queue while set is playing");
                return false;
            }

            _songQueue.Clear();
            _currentSongIndex = -1;

            Debug.Log("Queue cleared");

            OnQueueChanged?.Invoke();

            // TODO: Broadcast to all clients in multiplayer
            return true;
        }

        /// <summary>
        /// Start playing the queued songs as a set. Host only.
        /// </summary>
        public bool StartSet()
        {
            if (!IsHost())
            {
                Debug.LogWarning("Only host can start set");
                return false;
            }

            if (_songQueue.Count == 0)
            {
                Debug.LogWarning("Cannot start set with empty queue");
                return false;
            }

            if (_isPlayingSet)
            {
                Debug.LogWarning("Set is already playing");
                return false;
            }

            _isPlayingSet = true;
            _currentSongIndex = 0;

            Debug.Log($"Starting set with {_songQueue.Count} songs");

            OnSetStarted?.Invoke();

            // Start first song
            PlayCurrentSong();

            // TODO: Broadcast to all clients in multiplayer
            return true;
        }

        /// <summary>
        /// Move to the next song in the set.
        /// </summary>
        public bool NextSong()
        {
            if (!_isPlayingSet)
            {
                Debug.LogWarning("No set is currently playing");
                return false;
            }

            if (!HasNextSong)
            {
                Debug.Log("No more songs in set, ending set");
                EndSet();
                return false;
            }

            _currentSongIndex++;
            PlayCurrentSong();

            return true;
        }

        /// <summary>
        /// End the current set and return to queue mode.
        /// </summary>
        public void EndSet()
        {
            if (!_isPlayingSet)
            {
                return;
            }

            Debug.Log("Set ended");

            _isPlayingSet = false;
            _currentSongIndex = -1;

            // Clear played songs
            _songQueue.Clear();

            OnSetEnded?.Invoke();
            OnQueueChanged?.Invoke();

            // TODO: Broadcast to all clients in multiplayer
            // TODO: Return all players to MusicLibrary for more queueing
        }

        /// <summary>
        /// Get the currently playing song.
        /// </summary>
        public QueuedSong GetCurrentSong()
        {
            if (!_isPlayingSet || _currentSongIndex < 0 || _currentSongIndex >= _songQueue.Count)
            {
                return null;
            }

            return _songQueue[_currentSongIndex];
        }

        /// <summary>
        /// Get the next song that will play.
        /// </summary>
        public QueuedSong GetNextSong()
        {
            if (!_isPlayingSet || !HasNextSong)
            {
                return null;
            }

            return _songQueue[_currentSongIndex + 1];
        }

        private void PlayCurrentSong()
        {
            var current = GetCurrentSong();
            if (current == null)
            {
                Debug.LogError("No current song to play");
                return;
            }

            Debug.Log($"Playing song {_currentSongIndex + 1}/{_songQueue.Count}: {current.song.Name}");

            OnSongStarting?.Invoke(current.song);

            // TODO: Load gameplay scene with current song
            // TODO: Broadcast to all clients to also load song
        }

        private bool IsHost()
        {
            return YargNetworkManager.Instance != null && 
                   YargNetworkManager.Instance.IsHosting;
        }

        private void OnLobbyLeft()
        {
            // Clear queue when leaving lobby
            _songQueue.Clear();
            _currentSongIndex = -1;
            _isPlayingSet = false;

            Debug.Log("Lobby left, queue cleared");
            OnQueueChanged?.Invoke();
        }

        // Debug/Testing methods
        public void DebugPrintQueue()
        {
            Debug.Log($"=== Song Queue ({_songQueue.Count} songs) ===");
            for (int i = 0; i < _songQueue.Count; i++)
            {
                var q = _songQueue[i];
                string current = (_isPlayingSet && i == _currentSongIndex) ? " [CURRENT]" : "";
                Debug.Log($"{i + 1}. {q.song.Name} (queued by {q.queuedByPlayer}){current}");
            }
        }
    }
}
