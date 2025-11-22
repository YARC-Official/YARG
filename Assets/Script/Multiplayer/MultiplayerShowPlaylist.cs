using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;
using YARG.Core.Song;
using YARG.Playback;
using YARG.Playlists;
using YARG.Song;
using YARG.Networking;

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

        private void ApplyPlaylistToGlobalState(bool updateCurrentSong)
        {
            GlobalVariables.State.ShowSongs = _localShowPlaylist.ToList();

            if (!updateCurrentSong)
            {
                return;
            }

            if (GlobalVariables.State.ShowSongs.Count == 0)
            {
                GlobalVariables.State.ShowIndex = 0;
                GlobalVariables.State.CurrentSong = null;
                return;
            }

            GlobalVariables.State.ShowIndex = Mathf.Clamp(
                GlobalVariables.State.ShowIndex,
                0,
                GlobalVariables.State.ShowSongs.Count - 1);
            GlobalVariables.State.CurrentSong = GlobalVariables.State.ShowSongs[GlobalVariables.State.ShowIndex];
        }

        private void OnShowPlaylistChanged(string oldValue, string newValue)
        {
            Debug.Log($"[MultiplayerShowPlaylist] OnShowPlaylistChanged hook fired. Old: '{oldValue}', New: '{newValue}'");

            if (!isClient)
            {
                Debug.Log("[MultiplayerShowPlaylist] Running on server-only instance; skipping client-side deserialization");
                return;
            }

            DeserializeShowPlaylist(newValue);
            _hasReceivedInitialSync = true;
            ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
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
                ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
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
                ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
                
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
            ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
            
            // Trigger update event so UI refreshes
            OnPlaylistUpdated?.Invoke();
        }

        /// <summary>
        /// Called by any player to add a song to the show (networked)
        /// </summary>
        public bool IsInPlaylist(string songHash)
        {
            var hash = HashWrapper.FromString(songHash);
            return _localShowPlaylist.ContainsSong(hash);
        }

        public bool HasReceivedInitialSync => _hasReceivedInitialSync;

        [Command(requiresAuthority = false)]
        public void CmdAddSongToShow(string songHash, string playerName, string songName, string songArtist)
        {
            Debug.Log($"[MultiplayerShowPlaylist] CmdAddSongToShow received for hash: {songHash} from player: {playerName}");
            var hashWrapper = HashWrapper.FromString(songHash);
            if (!_localShowPlaylist.ContainsSong(hashWrapper))
            {
                string resolvedName = songName;
                string resolvedArtist = songArtist;

                if (SongContainer.SongsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    var song = songList[0];
                    _localShowPlaylist.AddSong(song);
                    resolvedName = song.Name;
                    resolvedArtist = song.Artist;
                    Debug.Log($"[MultiplayerShowPlaylist] Added song '{resolvedName}' to show playlist (now {_localShowPlaylist.Count} songs)");
                }
                else if (_localShowPlaylist.AddSong(hashWrapper))
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Song hash {songHash} not found on server. Added by hash only.");
                    if (string.IsNullOrWhiteSpace(resolvedName))
                    {
                        resolvedName = songHash;
                    }
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Failed to add song hash {songHash}; already present or invalid.");
                    return;
                }

                ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
                SyncShowPlaylistToClients();

                // Notify all players via toast
                if (string.IsNullOrWhiteSpace(resolvedArtist))
                {
                    resolvedArtist = "Unknown Artist";
                }
                RpcNotifySongAdded(playerName, resolvedName, resolvedArtist);
                
                Debug.Log($"[MultiplayerShowPlaylist] Synced playlist to clients: {showPlaylistSerialized}");
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
        public void CmdRemoveSongFromShow(string songHash, string playerName, string songName, string songArtist)
        {
            Debug.Log($"[MultiplayerShowPlaylist] CmdRemoveSongFromShow received for hash: {songHash} from player: {playerName}");
            var hashWrapper = HashWrapper.FromString(songHash);
            if (_localShowPlaylist.ContainsSong(hashWrapper))
            {
                // Find the song entry and use proper RemoveSong method
                string resolvedName = songName;
                string resolvedArtist = songArtist;

                if (SongContainer.SongsByHash.TryGetValue(hashWrapper, out var songList) && songList.Count > 0)
                {
                    var song = songList[0];
                    resolvedName = song.Name;
                    resolvedArtist = song.Artist;
                    _localShowPlaylist.RemoveSong(song);
                    Debug.Log($"[MultiplayerShowPlaylist] Removed song '{resolvedName}' from show playlist (now {_localShowPlaylist.Count} songs)");
                }
                else if (_localShowPlaylist.RemoveSong(hashWrapper))
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Song hash {songHash} not found on server. Removed by hash only.");
                    if (string.IsNullOrWhiteSpace(resolvedName))
                    {
                        resolvedName = songHash;
                    }
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerShowPlaylist] Failed to remove song hash {songHash}; not present.");
                    return;
                }

                ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
                SyncShowPlaylistToClients();
                
                // Notify all players via toast
                if (string.IsNullOrWhiteSpace(resolvedArtist))
                {
                    resolvedArtist = "Unknown Artist";
                }
                RpcNotifySongRemoved(playerName, resolvedName, resolvedArtist);
                
                Debug.Log($"[MultiplayerShowPlaylist] Synced playlist to clients: {showPlaylistSerialized}");
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
            var networkManager = YargNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[MultiplayerShowPlaylist] Cannot verify host - network manager missing");
                return;
            }

            if (!networkManager.ConnectionIsHost(sender))
            {
                Debug.LogWarning("[MultiplayerShowPlaylist] Non-host tried to start show");
                return;
            }

            if (_localShowPlaylist.Count > 0)
            {
                Debug.Log($"[MultiplayerShowPlaylist] Host starting show with {_localShowPlaylist.Count} songs");

                // Ensure latest playlist serialization before notifying clients
                SyncShowPlaylistToClients();

                // Navigate host to difficulty select, but skip menu interaction on dedicated servers
                if (networkManager.IsDedicatedServer)
                {
                    Debug.Log("[MultiplayerShowPlaylist] Dedicated server starting show without local navigation");
                    GlobalVariables.State.PlayingAShow = true;
                    GlobalVariables.State.ShowIndex = 0;
                    ApplyPlaylistToGlobalState(updateCurrentSong: true);
                }
                else
                {
                    NavigateToDifficultySelect();
                }

                // Navigate all clients to difficulty select with explicit playlist data
                RpcStartShow(showPlaylistSerialized);
            }
        }
        
        private void NavigateToDifficultySelect()
        {
            GlobalVariables.State.PlayingAShow = true;
            GlobalVariables.State.ShowIndex = 0;
            ApplyPlaylistToGlobalState(updateCurrentSong: true);
            
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
                ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
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
        public void CmdClearShowPlaylist(NetworkConnectionToClient sender = null)
        {
            var networkManager = YargNetworkManager.Instance;
            if (networkManager == null)
            {
                Debug.LogError("[MultiplayerShowPlaylist] Cannot clear playlist - network manager missing");
                return;
            }

            if (!networkManager.ConnectionIsHost(sender))
            {
                Debug.LogWarning("[MultiplayerShowPlaylist] Non-host tried to clear the show playlist");
                return;
            }

            _localShowPlaylist.Clear();
            SyncShowPlaylistToClients();
            Debug.Log("[MultiplayerShowPlaylist] Cleared show playlist");
            ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
        }

        public bool HostRemoveSong(SongEntry song)
        {
            if (song == null)
            {
                return false;
            }

            return HostRemoveSong(song.Hash);
        }

        public bool HostRemoveSong(HashWrapper hash)
        {
            if (!isServer)
            {
                return false;
            }

            if (!_localShowPlaylist.ContainsSong(hash))
            {
                Debug.LogWarning($"[MultiplayerShowPlaylist] Attempted to remove song hash {hash} that is not in the show playlist.");
                return false;
            }

            if (!_localShowPlaylist.RemoveSong(hash))
            {
                Debug.LogWarning($"[MultiplayerShowPlaylist] Failed to remove song hash {hash} from the show playlist.");
                return false;
            }

            ApplyPlaylistToGlobalState(GlobalVariables.State.PlayingAShow);
            SyncShowPlaylistToClients();
            Debug.Log($"[MultiplayerShowPlaylist] Host removed song hash {hash}; playlist now has {_localShowPlaylist.Count} songs");
            return true;
        }
    }
}
