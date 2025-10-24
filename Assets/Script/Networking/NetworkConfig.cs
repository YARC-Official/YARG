using System;
using UnityEngine;

namespace YARG.Networking
{
    /// <summary>
    /// Configuration data for network settings and lobby preferences.
    /// </summary>
    [Serializable]
    public class NetworkConfig
    {
        [Header("Player Settings")]
        public string playerName = "Player";

        [Header("Lobby Settings")]
        public int defaultMaxPlayers = 32;
        public int maxLocalPlayersPerClient = 4;
        public int maxDisplayedPlayers = 4;
        public YargNetworkManager.LobbyPrivacyMode defaultPrivacyMode = YargNetworkManager.LobbyPrivacyMode.Public;

        [Header("Network Settings")]
        public int connectionTimeout = 30;
        public int discoveryPort = 47777;
        public float discoveryInterval = 2f;

        [Header("Gameplay Settings")]
        public bool allowMidGameJoin = false;
        public bool syncAudioLocally = true;
        public int networkTickRate = 60;
    }

    /// <summary>
    /// Lobby creation settings.
    /// </summary>
    [Serializable]
    public class LobbySettings
    {
        public string lobbyName;
        public int maxPlayers;
        public YargNetworkManager.LobbyPrivacyMode privacyMode;
        public string password;
        public bool allowMidGameJoin;
        public int minPlayers;

        public LobbySettings()
        {
            lobbyName = "YARG Lobby";
            maxPlayers = 32;
            privacyMode = YargNetworkManager.LobbyPrivacyMode.Public;
            password = string.Empty;
            allowMidGameJoin = false;
            minPlayers = 1;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(lobbyName) 
                   && maxPlayers > 0 
                   && maxPlayers <= 32
                   && minPlayers > 0 
                   && minPlayers <= maxPlayers;
        }
    }
}