using Mirror;
using UnityEngine;

namespace YARG.Networking
{
    /// <summary>
    /// NetworkBehaviour component to sync lobby info from server to clients.
    /// Attached to the YargNetworkManager GameObject.
    /// </summary>
    public class LobbyInfoSync : NetworkBehaviour
    {
        /// <summary>
        /// Called by server to sync lobby info to a specific client.
        /// </summary>
        [TargetRpc]
        public void RpcSyncLobbyInfo(NetworkConnectionToClient target, string lobbyName, string hostName, 
            int maxPlayers, bool hasPassword, int privacyMode)
        {
            Debug.Log($"[LobbyInfoSync] Received lobby info from server: {lobbyName} hosted by {hostName}");
            
            if (YargNetworkManager.Instance != null && YargNetworkManager.Instance.CurrentLobby != null)
            {
                var lobby = YargNetworkManager.Instance.CurrentLobby;
                lobby.lobbyName = lobbyName;
                lobby.hostName = hostName;
                lobby.maxPlayers = maxPlayers;
                lobby.hasPassword = hasPassword;
                lobby.privacyMode = (YargNetworkManager.LobbyPrivacyMode)privacyMode;
                lobby.currentPlayers = NetworkServer.active ? NetworkServer.connections.Count : 2;
                
                Debug.Log($"[LobbyInfoSync] Updated client lobby info: {lobby.lobbyName}");
                
                // Trigger update event so UI refreshes
                YargNetworkManager.Instance.TriggerLobbyJoinedEvent(lobby);
            }
        }
    }
}
