using System;
using UnityEngine;

namespace YARG.Networking.Bookmarks
{
    /// <summary>
    /// Snapshot of a lobby endpoint saved by the player.
    /// </summary>
    [Serializable]
    public sealed class LobbyBookmark
    {
        public string address = string.Empty;
        public int port;
        public string displayName = string.Empty;
        public string password = string.Empty;
        public bool favorite;
        public long lastConnected;
        public long createdAt;

        public string EndpointKey => LobbyBookmarkUtility.BuildKey(address, (ushort)Mathf.Clamp(port, 0, ushort.MaxValue));

        public LobbyBookmark Clone()
        {
            return new LobbyBookmark
            {
                address = address,
                port = port,
                displayName = displayName,
                password = password,
                favorite = favorite,
                lastConnected = lastConnected,
                createdAt = createdAt
            };
        }
    }
}
