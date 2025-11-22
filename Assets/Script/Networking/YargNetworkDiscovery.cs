using Mirror;
using Mirror.Discovery;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using kcp2k;

namespace YARG.Networking
{
    /// <summary>
    /// Network discovery for finding lobbies on the local network.
    /// Allows players to discover and join lobbies without knowing IP addresses.
    /// </summary>
    public class YargNetworkDiscovery : NetworkDiscoveryBase<ServerRequest, ServerResponse>
    {
        /// <summary>
        /// Exposes the configured UDP port used for discovery requests.
        /// </summary>
        public int DiscoveryPort => serverBroadcastListenPort;

        /// <summary>
        /// Send a direct discovery request to a specific address/port.
        /// </summary>
        public void SendDiscoveryRequest(string address, int port = 0)
        {
            if (clientUdpClient == null)
                return;

            if (NetworkClient.isConnected)
            {
                StopDiscovery();
                return;
            }

            if (string.IsNullOrWhiteSpace(address))
            {
                Debug.LogWarning("[YargNetworkDiscovery] Cannot send discovery request without an address.");
                return;
            }

            int targetPort = port > 0 ? port : serverBroadcastListenPort;
            if (targetPort <= 0)
            {
                Debug.LogWarning($"[YargNetworkDiscovery] Invalid discovery port resolved for '{address}'.");
                return;
            }

            IPAddress ipAddress;
            if (!IPAddress.TryParse(address, out ipAddress))
            {
                try
                {
                    var addresses = Dns.GetHostAddresses(address);
                    ipAddress = addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork) ?? addresses.FirstOrDefault();
                    if (ipAddress == null)
                    {
                        Debug.LogWarning($"[YargNetworkDiscovery] DNS lookup returned no usable addresses for {address}.");
                        return;
                    }
                }
                catch (Exception dnsEx)
                {
                    Debug.LogWarning($"[YargNetworkDiscovery] DNS lookup failed for {address}: {dnsEx.Message}");
                    return;
                }
            }

            try
            {
                targetPort = Mathf.Clamp(targetPort, 1, ushort.MaxValue);
                var endPoint = new IPEndPoint(ipAddress, targetPort);
                using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                {
                    writer.WriteLong(secretHandshake);
                    ServerRequest request = new ServerRequest();
                    writer.Write(request);
                    ArraySegment<byte> data = writer.ToArraySegment();
                    clientUdpClient.SendAsync(data.Array, data.Count, endPoint);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YargNetworkDiscovery] Failed to send direct discovery request to {ipAddress}:{targetPort}: {ex.Message}");
            }
        }
        private Dictionary<long, YargNetworkManager.LobbyInfo> _discoveredLobbies = new Dictionary<long, YargNetworkManager.LobbyInfo>();
        private YargNetworkManager.LobbyInfo _advertisedLobby;
        private float _lastCleanup = 0f;
        private const float CLEANUP_INTERVAL = 5f;
        private const float LOBBY_TIMEOUT = 15f;

        private KcpTransport _sharedPortTransport;
        private bool _sharedPortHooked;
        private bool _allowAdvertisement = true;

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
        public override void Start()
        {
            base.Start();

            if (!_allowAdvertisement)
            {
                StopDiscovery();
            }
        }

        public new void StartDiscovery()
        {
            _discoveredLobbies.Clear();
            base.StartDiscovery();
            Debug.Log($"Started lobby discovery (port {serverBroadcastListenPort})");
        }

        public void ConfigureDiscoveryOptions(bool enableAdvertisement, int? portOverride)
        {
            _allowAdvertisement = enableAdvertisement;
            if (!enableAdvertisement)
            {
                DisableSharedPortDiscovery();
                StopDiscovery();
            }

            if (portOverride.HasValue && portOverride.Value > 0)
            {
                serverBroadcastListenPort = (ushort)Mathf.Clamp(portOverride.Value, 1, ushort.MaxValue);
            }
        }

        /// <summary>
        /// Stop discovering lobbies.
        /// </summary>
        public new void StopDiscovery()
        {
            base.StopDiscovery();
            DisableSharedPortDiscovery();
            Debug.Log("Stopped lobby discovery");
        }

        /// <summary>
        /// Advertise this server's lobby.
        /// </summary>
        public void AdvertiseServer(YargNetworkManager.LobbyInfo lobby)
        {
            _advertisedLobby = lobby;
            if (!_allowAdvertisement)
            {
                Debug.Log("[YargNetworkDiscovery] Discovery disabled by configuration; skipping advertisement.");
                return;
            }

            try
            {
                base.AdvertiseServer();
                EnableSharedPortDiscovery();
                Debug.Log($"Advertising lobby: {lobby.lobbyName} (discovery port {serverBroadcastListenPort})");
            }
            catch (SocketException ex)
            {
                Debug.LogWarning($"[YargNetworkDiscovery] Failed to bind discovery UDP port {serverBroadcastListenPort}: {ex.Message}. Discovery will be disabled.");
                DisableSharedPortDiscovery();
                _allowAdvertisement = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YargNetworkDiscovery] Unexpected error while advertising lobby: {ex.Message}");
            }
        }

        #region Server (Host)

        protected override void ProcessClientRequest(ServerRequest request, System.Net.IPEndPoint endpoint)
        {
            if (_advertisedLobby == null || !_advertisedLobby.isActive)
            {
                return;
            }

            base.ProcessClientRequest(request, endpoint);
        }

        protected override ServerResponse ProcessRequest(ServerRequest request, System.Net.IPEndPoint endpoint)
        {
            // Host responds to discovery requests
            var lobbySnapshot = _advertisedLobby;
            if (lobbySnapshot == null || !lobbySnapshot.isActive)
            {
                return default;
            }

            // Don't advertise private lobbies unless they provide a password (we allow
            // password-protected private lobbies to be discoverable so clients can get
            // basic stats and prompt for a password when joining).
            if (lobbySnapshot.privacyMode == YargNetworkManager.LobbyPrivacyMode.Private && !lobbySnapshot.hasPassword)
            {
                return default;
            }

            // Gather player info
            string[] playerNames = null;
            int[] playerInstruments = null;
            if (YargNetworkManager.Instance != null)
            {
                var players = YargNetworkManager.Instance.ConnectedPlayers
                    .SelectMany(kvp => kvp.Value)
                    .Where(p => p != null)
                    .ToList();
                playerNames = players.Select(p => p.PlayerName).ToArray();
                playerInstruments = players.Select(p => p.Instrument).ToArray();
            }

            ServerResponse response = new ServerResponse
            {
                lobbyId = lobbySnapshot.lobbyId,
                lobbyName = lobbySnapshot.lobbyName,
                hostName = lobbySnapshot.hostName,
                currentPlayers = YargNetworkManager.Instance != null ? YargNetworkManager.Instance.GetTotalPlayerCount() : 0,
                maxPlayers = lobbySnapshot.maxPlayers,
                hasPassword = lobbySnapshot.hasPassword,
                privacyMode = (int)lobbySnapshot.privacyMode,
                serverId = ServerId,
                publicAddress = lobbySnapshot.publicAddress,
                port = (ushort)Mathf.Clamp(lobbySnapshot.port, 0, ushort.MaxValue),
                publicPort = (ushort)Mathf.Clamp(lobbySnapshot.publicPort, 0, ushort.MaxValue),
                transportId = lobbySnapshot.transportId ?? string.Empty,
                playerNames = playerNames,
                playerInstruments = playerInstruments
            };

            Debug.Log($"[YargNetworkDiscovery] Responding to discovery request: lobby='{response.lobbyName}', host='{response.hostName}', hasPassword={response.hasPassword}, players={response.currentPlayers}");
            return response;
        }

        #endregion

        #region Client

        protected override void ProcessResponse(ServerResponse response, System.Net.IPEndPoint endpoint)
        {
            // Client receives lobby information
            int resolvedPort = response.port;
            if (resolvedPort <= 0)
            {
                resolvedPort = NetworkTransportDefaults.DefaultUdpPort;
            }

            int resolvedPublicPort = response.publicPort;
            if (resolvedPublicPort <= 0)
            {
                resolvedPublicPort = resolvedPort;
            }

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
                // Use milliseconds for lastSeen to match other code expectations
                lastSeen = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                port = (ushort)Mathf.Clamp(resolvedPort, 0, ushort.MaxValue),
                publicPort = (ushort)Mathf.Clamp(resolvedPublicPort, 0, ushort.MaxValue),
                publicAddress = string.IsNullOrWhiteSpace(response.publicAddress) ? endpoint.Address.ToString() : response.publicAddress,
                transportId = response.transportId ?? string.Empty,
                playerNames = response.playerNames,
                playerInstruments = response.playerInstruments
            };

            // Add or update lobby
            bool isNew = !_discoveredLobbies.ContainsKey(response.serverId);
            _discoveredLobbies[response.serverId] = lobby;

            // Always invoke OnLobbyDiscovered for direct ping responses
            Debug.Log($"Discovered lobby: {lobby.lobbyName} at {lobby.ipAddress}");
            OnLobbyDiscovered?.Invoke(lobby);

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

        private void EnableSharedPortDiscovery()
        {
            if (_sharedPortHooked)
                return;

            Transport activeTransport = transport ?? Transport.active;
            if (activeTransport == null)
            {
                activeTransport = GetComponent<Transport>();
            }

            _sharedPortTransport = activeTransport as KcpTransport ?? GetComponent<KcpTransport>();
            if (_sharedPortTransport == null)
            {
                return;
            }

            _sharedPortTransport.ServerRawPacket += HandleSharedPortDiscoveryPacket;
            _sharedPortHooked = true;
        }

        private void DisableSharedPortDiscovery()
        {
            if (!_sharedPortHooked)
                return;

            if (_sharedPortTransport != null)
            {
                _sharedPortTransport.ServerRawPacket -= HandleSharedPortDiscoveryPacket;
            }

            _sharedPortTransport = null;
            _sharedPortHooked = false;
        }

        private bool HandleSharedPortDiscoveryPacket(ArraySegment<byte> payload, IPEndPoint endpoint)
        {
            if (_advertisedLobby == null || !_advertisedLobby.isActive)
            {
                return false;
            }

            if (payload.Count < sizeof(long))
            {
                return false;
            }

            try
            {
                using (NetworkReaderPooled reader = NetworkReaderPool.Get(payload))
                {
                    long handshake = reader.ReadLong();
                    if (handshake != secretHandshake)
                    {
                        return false;
                    }

                    ServerRequest request = reader.Read<ServerRequest>();
                    ServerResponse response = ProcessRequest(request, endpoint);

                    using (NetworkWriterPooled writer = NetworkWriterPool.Get())
                    {
                        writer.WriteLong(secretHandshake);
                        writer.Write(response);
                        ArraySegment<byte> data = writer.ToArraySegment();
                        if (_sharedPortTransport == null || !_sharedPortTransport.TrySendServerRaw(endpoint, data))
                        {
                            Debug.LogWarning($"[YargNetworkDiscovery] Failed to send shared-port discovery response to {endpoint}");
                        }
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[YargNetworkDiscovery] Exception while handling shared-port discovery packet: {ex.Message}");
                return true;
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
        public string transportId;

        // New fields for player info
        public string[] playerNames;
        public int[] playerInstruments;
    }
}