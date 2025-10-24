using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace YARG.Multiplayer
{
    public struct SongQueueEntry
    {
        public string songId;
        public string songName;
        // Add other metadata as needed (difficulty, etc.)
    }

    public class MultiplayerSongQueue : NetworkBehaviour
    {
        [SyncVar]
        public bool isSetActive = false;

        [SyncVar]
        public int currentSongIndex = -1;

        [SyncVar]
        public string hostId;

        [SyncVar]
        public string queueSerialized;

        private List<SongQueueEntry> _queue = new List<SongQueueEntry>();

        public IReadOnlyList<SongQueueEntry> Queue => _queue;

        public void AddSongToQueue(SongQueueEntry entry)
        {
            if (!isServer) return;
            _queue.Add(entry);
            SyncQueueToClients();
        }

        public void RemoveSongFromQueue(int index)
        {
            if (!isServer) return;
            if (index >= 0 && index < _queue.Count)
            {
                _queue.RemoveAt(index);
                SyncQueueToClients();
            }
        }

        public void ClearQueue()
        {
            if (!isServer) return;
            _queue.Clear();
            SyncQueueToClients();
        }

        public void StartSet()
        {
            if (!isServer || _queue.Count == 0) return;
            isSetActive = true;
            currentSongIndex = 0;
            RpcStartSet(_queue[currentSongIndex].songId);
        }

        public void AdvanceToNextSong()
        {
            if (!isServer || !_queue.Count.Equals(currentSongIndex + 1))
            {
                currentSongIndex++;
                if (currentSongIndex < _queue.Count)
                {
                    RpcStartSet(_queue[currentSongIndex].songId);
                }
                else
                {
                    isSetActive = false;
                    currentSongIndex = -1;
                    RpcEndSet();
                }
            }
        }

        [ClientRpc]
        private void RpcStartSet(string songId)
        {
            // TODO: Hook into gameplay manager to start song by ID
            Debug.Log($"Starting song: {songId}");
        }

        [ClientRpc]
        private void RpcEndSet()
        {
            // TODO: Notify UI/gameplay that set is complete
            Debug.Log("Song set complete");
        }

        private void SyncQueueToClients()
        {
            // Serialize queue for SyncVar
            queueSerialized = SerializeQueue(_queue);
        }

        private string SerializeQueue(List<SongQueueEntry> queue)
        {
            // Simple serialization (replace with JSON if needed)
            return string.Join("|", queue.ConvertAll(e => e.songId + "," + e.songName));
        }

        private List<SongQueueEntry> DeserializeQueue(string data)
        {
            var result = new List<SongQueueEntry>();
            if (string.IsNullOrEmpty(data)) return result;
            var entries = data.Split('|');
            foreach (var entry in entries)
            {
                var parts = entry.Split(',');
                if (parts.Length >= 2)
                {
                    result.Add(new SongQueueEntry { songId = parts[0], songName = parts[1] });
                }
            }
            return result;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _queue = DeserializeQueue(queueSerialized);
        }
    }
}
