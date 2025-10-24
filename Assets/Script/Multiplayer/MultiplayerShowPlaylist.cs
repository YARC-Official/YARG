using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using YARG.Playlists;
using YARG.Song;

namespace YARG.Multiplayer
{
    /// <summary>
    /// Manages the multiplayer show playlist/setlist synchronization.
    /// Allows all players to add/remove songs, but only the host can start the show.
    /// </summary>
    public class MultiplayerShowPlaylist : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnShowPlaylistChanged))]
        private string showPlaylistSerialized = "";

        private Playlist _localShowPlaylist;

        public Playlist ShowPlaylist
        {
            get => _localShowPlaylist;
            set => _localShowPlaylist = value;
        }

        private void Awake()
        {
            _localShowPlaylist = new Playlist(true);
        }

        private void OnShowPlaylistChanged(string oldValue, string newValue)
        {
            Debug.Log($"[MultiplayerShowPlaylist] OnShowPlaylistChanged hook fired. Old: '{oldValue}', New: '{newValue}'");
            DeserializeShowPlaylist(newValue);
            _hasReceivedInitialSync = true;
            Debug.Log($"[MultiplayerShowPlaylist] After deserialization, playlist has {_localShowPlaylist.Count} songs");
            OnPlaylistUpdated?.Invoke();
        }

        public event System.Action OnPlaylistUpdated;

        private void SyncShowPlaylistToClients()
        {
            if (!isServer) return;
            showPlaylistSerialized = SerializeShowPlaylist(_localShowPlaylist);
        }

        private string SerializeShowPlaylist(Playlist playlist)
        {
            // Simple serialization: songHash1|songHash2|...
            return string.Join("|", playlist.SongHashes.Select(h => h.ToString()));
        }

        private void DeserializeShowPlaylist(string data)
        {
            _localShowPlaylist.Clear();
            if (string.IsNullOrEmpty(data)) return;
            
            var hashes = data.Split('|');
            foreach (var hash in hashes)
            {
                if (string.IsNullOrWhiteSpace(hash)) continue;
                
                var hashWrapper = YARG.Core.Song.HashWrapper.FromString(hash);
                if (SongContainer.SongsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    _localShowPlaylist.AddSong(songList[0]);
                }
            }
        }

        private bool _hasReceivedInitialSync = false;

        public override void OnStartClient()
        {
            base.OnStartClient();
            Debug.Log($"[MultiplayerShowPlaylist] OnStartClient called, isServer: {isServer}, deserializing: {showPlaylistSerialized}");
            
            // Host doesn't need to wait for SyncVar - it already has the local playlist
            if (isServer)
            {
                Debug.Log("[MultiplayerShowPlaylist] Host skipping SyncVar wait - using local playlist");
                _hasReceivedInitialSync = true;
                OnPlaylistUpdated?.Invoke();
                return;
            }
            
            // Client: If SyncVar appears empty, wait a bit for it to sync from server
            if (string.IsNullOrEmpty(showPlaylistSerialized))
            {
                Debug.Log("[MultiplayerShowPlaylist] Client SyncVar empty on OnStartClient, starting delayed sync...");
                StartCoroutine(WaitForInitialSync());
            }
            else
            {
                DeserializeShowPlaylist(showPlaylistSerialized);
                Debug.Log($"[MultiplayerShowPlaylist] Client playlist now has {_localShowPlaylist.Count} songs");
                _hasReceivedInitialSync = true;
                
                // Trigger update event so UI refreshes
                OnPlaylistUpdated?.Invoke();
            }
        }
        
        private System.Collections.IEnumerator WaitForInitialSync()
        {
            int attempts = 0;
            while (string.IsNullOrEmpty(showPlaylistSerialized) && attempts < 50) // Wait up to 5 seconds
            {
                attempts++;
                yield return new WaitForSeconds(0.1f);
            }
            
            Debug.Log($"[MultiplayerShowPlaylist] After {attempts} attempts, deserializing: {showPlaylistSerialized}");
            DeserializeShowPlaylist(showPlaylistSerialized);
            Debug.Log($"[MultiplayerShowPlaylist] Client playlist now has {_localShowPlaylist.Count} songs");
            _hasReceivedInitialSync = true;
            
            // Trigger update event so UI refreshes
            OnPlaylistUpdated?.Invoke();
        }

        /// <summary>
        /// Called by any player to add a song to the show (networked)
        /// </summary>
        public bool IsInPlaylist(string songHash)
        {
            var hash = YARG.Core.Song.HashWrapper.FromString(songHash);
            return _localShowPlaylist.SongHashes.Contains(hash);
        }

        public bool HasReceivedInitialSync => _hasReceivedInitialSync;

        [Command(requiresAuthority = false)]
        public void CmdAddSongToShow(string songHash, string playerName)
        {
            Debug.Log($"[MultiplayerShowPlaylist] CmdAddSongToShow received for hash: {songHash} from player: {playerName}");
            var hashWrapper = YARG.Core.Song.HashWrapper.FromString(songHash);
            if (!_localShowPlaylist.SongHashes.Contains(hashWrapper))
            {
                if (SongContainer.SongsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    _localShowPlaylist.AddSong(songList[0]);
                    Debug.Log($"[MultiplayerShowPlaylist] Added song '{songList[0].Name}' to show playlist (now {_localShowPlaylist.Count} songs)");
                    SyncShowPlaylistToClients();
                    
                    // Notify all players via toast
                    RpcNotifySongAdded(playerName, songList[0].Name, songList[0].Artist);
                    
                    Debug.Log($"[MultiplayerShowPlaylist] Synced playlist to clients: {showPlaylistSerialized}");
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Song hash {songHash} not found in SongContainer");
                }
            }
            else
            {
                Debug.Log($"[MultiplayerShowPlaylist] Song {songHash} already in playlist, skipping");
            }
        }

        /// <summary>
        /// Called by any player to remove a song from the show (networked)
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdRemoveSongFromShow(string songHash, string playerName)
        {
            Debug.Log($"[MultiplayerShowPlaylist] CmdRemoveSongFromShow received for hash: {songHash} from player: {playerName}");
            var hashWrapper = YARG.Core.Song.HashWrapper.FromString(songHash);
            if (_localShowPlaylist.SongHashes.Contains(hashWrapper))
            {
                // Find the song entry and use proper RemoveSong method
                if (SongContainer.SongsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    var songName = songList[0].Name;
                    var artist = songList[0].Artist;
                    _localShowPlaylist.RemoveSong(songList[0]);
                    Debug.Log($"[MultiplayerShowPlaylist] Removed song '{songName}' from show playlist (now {_localShowPlaylist.Count} songs)");
                    SyncShowPlaylistToClients();
                    
                    // Notify all players via toast
                    RpcNotifySongRemoved(playerName, songName, artist);
                    
                    Debug.Log($"[MultiplayerShowPlaylist] Synced playlist to clients: {showPlaylistSerialized}");
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Song hash {songHash} not found in SongContainer for removal");
                }
            }
            else
            {
                Debug.Log($"[MultiplayerShowPlaylist] Song {songHash} not in playlist, skipping removal");
            }
        }

        /// <summary>
        /// Only host can start the show
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdStartShow(NetworkConnectionToClient sender = null)
        {
            // Verify sender is the host
            if (sender != null && sender != NetworkServer.localConnection)
            {
                Debug.LogWarning("[MultiplayerShowPlaylist] Non-host tried to start show");
                return;
            }

            if (_localShowPlaylist.Count > 0)
            {
                Debug.Log($"[MultiplayerShowPlaylist] Host starting show with {_localShowPlaylist.Count} songs");

                // Ensure latest playlist serialization before notifying clients
                SyncShowPlaylistToClients();

                // Navigate host to difficulty select
                NavigateToDifficultySelect();

                // Navigate all clients to difficulty select with explicit playlist data
                RpcStartShow(showPlaylistSerialized);
            }
        }
        
        private void NavigateToDifficultySelect()
        {
            GlobalVariables.State.PlayingAShow = true;
            GlobalVariables.State.ShowSongs = _localShowPlaylist.ToList();
            GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs.First();
            GlobalVariables.State.ShowIndex = 0;
            
            Debug.Log($"[MultiplayerShowPlaylist] Navigating to difficulty select with {GlobalVariables.State.ShowSongs.Count} songs. First song: {GlobalVariables.State.CurrentSong.Name}");
            YARG.Menu.MenuManager.Instance.PushMenu(YARG.Menu.MenuManager.Menu.DifficultySelect);
        }

        [ClientRpc]
        private void RpcNotifySongAdded(string playerName, string songName, string artist)
        {
            var message = $"{playerName} added '{songName}' by {artist}";
            Menu.Persistent.ToastManager.ToastInformation(message);
            Debug.Log($"[MultiplayerShowPlaylist] Toast: {message}");
        }

        [ClientRpc]
        private void RpcNotifySongRemoved(string playerName, string songName, string artist)
        {
            var message = $"{playerName} removed '{songName}' by {artist}";
            Menu.Persistent.ToastManager.ToastInformation(message);
            Debug.Log($"[MultiplayerShowPlaylist] Toast: {message}");
        }

        [ClientRpc]
        private void RpcStartShow(string serializedPlaylist)
        {
            // Host already navigated locally, skip RPC processing
            if (NetworkServer.active) return;

            Debug.Log($"[MultiplayerShowPlaylist] Client received start show RPC. Local playlist has {_localShowPlaylist.Count} songs");

            // Make sure we have the latest playlist data before navigating
            if (!string.IsNullOrEmpty(serializedPlaylist))
            {
                if (serializedPlaylist != showPlaylistSerialized)
                {
                    Debug.Log("[MultiplayerShowPlaylist] Updating SyncVar cache from RPC payload");
                    showPlaylistSerialized = serializedPlaylist;
                }

                DeserializeShowPlaylist(serializedPlaylist);
                _hasReceivedInitialSync = true;
                OnPlaylistUpdated?.Invoke();
                Debug.Log($"[MultiplayerShowPlaylist] After RPC deserialization, playlist has {_localShowPlaylist.Count} songs");
            }

            if (_localShowPlaylist.Count == 0)
            {
                Debug.LogError("[MultiplayerShowPlaylist] Playlist is empty in RpcStartShow! Cannot navigate to difficulty select.");
                return;
            }

            NavigateToDifficultySelect();
        }

        /// <summary>
        /// Clear the show playlist (host only)
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdClearShowPlaylist()
        {
            _localShowPlaylist.Clear();
            SyncShowPlaylistToClients();
            Debug.Log("[MultiplayerShowPlaylist] Cleared show playlist");
        }
    }
}
