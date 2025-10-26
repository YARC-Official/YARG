using Mirror;
using Mirror.Discovery;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Networking.STUN;

namespace YARG.Networking
{
    /// <summary>
    /// Network discovery for finding lobbies on the local network.
    /// Allows players to discover and join lobbies without knowing IP addresses.
    /// </summary>
    public class YargNetworkDiscovery : NetworkDiscoveryBase<ServerRequest, ServerResponse>
    {
        private Dictionary<long, YargNetworkManager.LobbyInfo> _discoveredLobbies = new Dictionary<long, YargNetworkManager.LobbyInfo>();
        private YargNetworkManager.LobbyInfo _advertisedLobby;
        private float _lastCleanup = 0f;
        private const float CLEANUP_INTERVAL = 5f;
        private const float LOBBY_TIMEOUT = 15f;

        public IReadOnlyDictionary<long, YargNetworkManager.LobbyInfo> DiscoveredLobbies => _discoveredLobbies;

        public event Action<YargNetworkManager.LobbyInfo> OnLobbyDiscovered;
        public event Action<long> OnLobbyLost;

        private void Update()
        {
            // Clean up old lobbies
            if (Time.time - _lastCleanup > CLEANUP_INTERVAL)
            {
                _lastCleanup = Time.time;
                CleanupOldLobbies();
            }
        }

        /// <summary>
        /// Start discovering lobbies on the network.
        /// </summary>
        public new void StartDiscovery()
        {
            _discoveredLobbies.Clear();
            base.StartDiscovery();
            Debug.Log("Started lobby discovery");
        }

        /// <summary>
        /// Stop discovering lobbies.
        /// </summary>
        public new void StopDiscovery()
        {
            base.StopDiscovery();
            Debug.Log("Stopped lobby discovery");
        }

        /// <summary>
        /// Advertise this server's lobby.
        /// </summary>
        public void AdvertiseServer(YargNetworkManager.LobbyInfo lobby)
        {
            _advertisedLobby = lobby;
            AdvertiseServer();
            Debug.Log($"Advertising lobby: {lobby.lobbyName}");
        }

        #region Server (Host)

        protected override ServerResponse ProcessRequest(ServerRequest request, System.Net.IPEndPoint endpoint)
        {
            // Host responds to discovery requests
            if (_advertisedLobby == null || !_advertisedLobby.isActive)
            {
                return default;
            }

            // Don't advertise private lobbies without password
            if (_advertisedLobby.privacyMode == YargNetworkManager.LobbyPrivacyMode.Private)
            {
                return default;
            }

            // Create response with lobby info
            ServerResponse response = new ServerResponse
            {
                lobbyId = _advertisedLobby.lobbyId,
                lobbyName = _advertisedLobby.lobbyName,
                hostName = _advertisedLobby.hostName,
                currentPlayers = YargNetworkManager.Instance != null ? YargNetworkManager.Instance.GetTotalPlayerCount() : 0,
                maxPlayers = _advertisedLobby.maxPlayers,
                hasPassword = _advertisedLobby.hasPassword,
                privacyMode = (int)_advertisedLobby.privacyMode,
                serverId = ServerId,
                publicAddress = _advertisedLobby.publicAddress,
                port = (ushort)Mathf.Clamp(_advertisedLobby.port, 0, ushort.MaxValue),
                publicPort = (ushort)Mathf.Clamp(_advertisedLobby.publicPort, 0, ushort.MaxValue),
                punchPort = (ushort)Mathf.Clamp(_advertisedLobby.punchPort, 0, ushort.MaxValue),
                natType = (byte)_advertisedLobby.natType,
                supportsNatTraversal = _advertisedLobby.supportsNatTraversal,
                transportId = _advertisedLobby.transportId ?? string.Empty,
                stunServer = _advertisedLobby.stunServer ?? string.Empty
            };

            return response;
        }

        #endregion

        #region Client

        protected override void ProcessResponse(ServerResponse response, System.Net.IPEndPoint endpoint)
        {
            // Client receives lobby information
            YargNetworkManager.LobbyInfo lobby = new YargNetworkManager.LobbyInfo
            {
                lobbyId = response.lobbyId,
                lobbyName = response.lobbyName,
                hostName = response.hostName,
                ipAddress = endpoint.Address.ToString(),
                currentPlayers = response.currentPlayers,
                maxPlayers = response.maxPlayers,
                hasPassword = response.hasPassword,
                privacyMode = (YargNetworkManager.LobbyPrivacyMode)response.privacyMode,
                isActive = true,
                lastSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                port = response.port != 0 ? response.port : NetworkTransportDefaults.DefaultTcpPort,
                publicPort = response.publicPort != 0 ? response.publicPort : response.port,
                punchPort = response.punchPort != 0 ? response.punchPort : NetworkTransportDefaults.DefaultUdpPort,
                natType = (NetworkNatType)response.natType,
                supportsNatTraversal = response.supportsNatTraversal,
                publicAddress = string.IsNullOrWhiteSpace(response.publicAddress) ? endpoint.Address.ToString() : response.publicAddress,
                transportId = response.transportId ?? string.Empty,
                stunServer = response.stunServer ?? string.Empty
            };

            // Add or update lobby
            bool isNew = !_discoveredLobbies.ContainsKey(response.serverId);
            _discoveredLobbies[response.serverId] = lobby;

            if (isNew)
            {
                Debug.Log($"Discovered lobby: {lobby.lobbyName} at {lobby.ipAddress}");
                OnLobbyDiscovered?.Invoke(lobby);
            }

            // Notify manager of updated lobby list
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.TriggerLobbyListUpdated(_discoveredLobbies.Values.ToList());
            }
        }

        #endregion

        private void CleanupOldLobbies()
        {
            long currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            List<long> lobbiesToRemove = new List<long>();

            foreach (var kvp in _discoveredLobbies)
            {
                if (currentTime - kvp.Value.lastSeen > LOBBY_TIMEOUT)
                {
                    lobbiesToRemove.Add(kvp.Key);
                }
            }

            foreach (long serverId in lobbiesToRemove)
            {
                _discoveredLobbies.Remove(serverId);
                OnLobbyLost?.Invoke(serverId);
                Debug.Log($"[Discovery] Lobby broadcast timed out (normal if already connected): {serverId}");
            }

            if (lobbiesToRemove.Count > 0 && YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.TriggerLobbyListUpdated(_discoveredLobbies.Values.ToList());
            }
        }

        /// <summary>
        /// Get list of all discovered lobbies.
        /// </summary>
        public List<YargNetworkManager.LobbyInfo> GetDiscoveredLobbies()
        {
            return _discoveredLobbies.Values.ToList();
        }

        /// <summary>
        /// Clear all discovered lobbies.
        /// </summary>
        public void ClearDiscoveredLobbies()
        {
            _discoveredLobbies.Clear();
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.TriggerLobbyListUpdated(new List<YargNetworkManager.LobbyInfo>());
            }
        }
    }

    /// <summary>
    /// Client request for server discovery.
    /// </summary>
    [Serializable]
    public struct ServerRequest : NetworkMessage
    {
        // Empty request - just looking for servers
    }

    /// <summary>
    /// Server response with lobby information.
    /// </summary>
    [Serializable]
    public struct ServerResponse : NetworkMessage
    {
        public string lobbyId;
        public string lobbyName;
        public string hostName;
        public int currentPlayers;
        public int maxPlayers;
        public bool hasPassword;
        public int privacyMode;
        public long serverId;
        public string publicAddress;
        public ushort port;
        public ushort publicPort;
        public ushort punchPort;
        public byte natType;
        public bool supportsNatTraversal;
        public string transportId;
        public string stunServer;
    }
}