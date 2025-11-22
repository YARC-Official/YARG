using System;
using UnityEngine;

namespace YARG.Networking.Bookmarks
{
    /// <summary>
    /// Stores a reusable lobby configuration created by the local player.
    /// These presets are surfaced in the "My Lobbies" section of the browser.
    /// </summary>
    [Serializable]
    public sealed class HostedLobbyPreset
    {
        public string id = string.Empty;
        public string lobbyName = string.Empty;
        public int maxPlayers = 8;
        public int privacyMode = (int)YargNetworkManager.LobbyPrivacyMode.Public;
        public string password = string.Empty;
        public long createdAt;
        public long lastHostedAt;

        /// <summary>
        /// Returns the preset's privacy mode as the strongly-typed enum.
        /// </summary>
        public YargNetworkManager.LobbyPrivacyMode PrivacyMode
        {
            get
            {
                var value = Mathf.Clamp(privacyMode, 0, Enum.GetValues(typeof(YargNetworkManager.LobbyPrivacyMode)).Length - 1);
                return (YargNetworkManager.LobbyPrivacyMode)value;
            }
            set => privacyMode = (int)value;
        }

        public HostedLobbyPreset Clone()
        {
            return new HostedLobbyPreset
            {
                id = id,
                lobbyName = lobbyName,
                maxPlayers = maxPlayers,
                privacyMode = privacyMode,
                password = password,
                createdAt = createdAt,
                lastHostedAt = lastHostedAt
            };
        }

        public void TouchHostedTimestamp()
        {
            lastHostedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        public void EnsureIdentifiers()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                id = Guid.NewGuid().ToString("N");
            }

            if (createdAt <= 0)
            {
                createdAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            }
        }
    }
}
