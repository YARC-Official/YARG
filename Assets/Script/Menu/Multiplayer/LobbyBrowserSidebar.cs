using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Networking;
using Cysharp.Text;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Sidebar for lobby browser showing detailed lobby information.
    /// Displays lobby name, host, player count, ping, password status, and player list with instruments.
    /// </summary>
    public class LobbyBrowserSidebar : MonoBehaviour
    {
        [Header("Lobby Info")]
        [SerializeField]
        private TextMeshProUGUI _lobbyNameText;
        [SerializeField]
        private TextMeshProUGUI _hostNameText;
        [SerializeField]
        private TextMeshProUGUI _playerCountText;
        [SerializeField]
        private TextMeshProUGUI _pingText;
        [SerializeField]
        private TextMeshProUGUI _privacyText;
        [SerializeField]
        private GameObject _passwordIcon;
        
        [Header("Player List")]
        [SerializeField]
        private Transform _playerListContainer;
        [SerializeField]
        private GameObject _playerEntryPrefab;
        [SerializeField]
        private TextMeshProUGUI _noPlayersText;
        
        [Header("Container")]
        [SerializeField]
        private GameObject _contentContainer;
        [SerializeField]
        private GameObject _emptyStateContainer;
        
        private LobbyBrowserMenu _menu;
        private YargNetworkManager.LobbyInfo _currentLobby;
        
        public void Initialize(LobbyBrowserMenu menu)
        {
            _menu = menu;
            ClearLobby();
        }
        
        public void SetLobby(YargNetworkManager.LobbyInfo lobby)
        {
            if (lobby == null)
            {
                ClearLobby();
                return;
            }
            
            _currentLobby = lobby;
            
            // Show content, hide empty state
            if (_contentContainer != null)
                _contentContainer.SetActive(true);
            if (_emptyStateContainer != null)
                _emptyStateContainer.SetActive(false);
            
            // Set lobby info
            if (_lobbyNameText != null)
            {
                _lobbyNameText.text = lobby.lobbyName;
            }
            
            if (_hostNameText != null)
            {
                _hostNameText.text = ZString.Format("Host: {0}", lobby.hostName);
            }
            
            if (_playerCountText != null)
            {
                var filledColor = lobby.currentPlayers >= lobby.maxPlayers 
                    ? new Color(1f, 0.3f, 0.3f) // Red when full
                    : MenuData.Colors.PrimaryText;
                
                var currentText = TextColorer.StyleString(
                    ZString.Format("{0}", lobby.currentPlayers),
                    filledColor,
                    600);
                
                var maxText = TextColorer.StyleString(
                    ZString.Format(" / {0} Players", lobby.maxPlayers),
                    MenuData.Colors.PrimaryText.WithAlpha(0.5f),
                    400);
                
                _playerCountText.text = ZString.Concat(currentText, maxText);
            }
            
            if (_pingText != null)
            {
                // Calculate ping (simplified for now)
                int ping = CalculatePing(lobby);
                Color pingColor;
                if (ping < 50)
                    pingColor = new Color(0.3f, 1f, 0.3f); // Green
                else if (ping < 100)
                    pingColor = new Color(1f, 1f, 0.3f); // Yellow
                else
                    pingColor = new Color(1f, 0.3f, 0.3f); // Red
                
                var pingValue = TextColorer.StyleString(ZString.Format("{0}", ping), pingColor, 600);
                var pingLabel = TextColorer.StyleString("ms", MenuData.Colors.PrimaryText.WithAlpha(0.5f), 400);
                _pingText.text = ZString.Concat("Ping: ", pingValue, pingLabel);
            }
            
            if (_privacyText != null)
            {
                string privacyMode = lobby.privacyMode switch
                {
                    YargNetworkManager.LobbyPrivacyMode.Public => "Public",
                    YargNetworkManager.LobbyPrivacyMode.Private => "Private",
                    YargNetworkManager.LobbyPrivacyMode.FriendsOnly => "Friends Only",
                    _ => "Unknown"
                };
                
                _privacyText.text = ZString.Format("Privacy: {0}", privacyMode);
            }
            
            if (_passwordIcon != null)
            {
                _passwordIcon.SetActive(lobby.hasPassword);
            }
            
            // Update player list
            UpdatePlayerList(lobby);
        }
        
        public void ClearLobby()
        {
            _currentLobby = null;
            
            // Show empty state, hide content
            if (_contentContainer != null)
                _contentContainer.SetActive(false);
            if (_emptyStateContainer != null)
                _emptyStateContainer.SetActive(true);
            
            // Clear player list
            ClearPlayerList();
        }
        
        private void UpdatePlayerList(YargNetworkManager.LobbyInfo lobby)
        {
            ClearPlayerList();
            
            // TODO: Get actual player list from NetworkPlayerData
            // For now, show placeholder based on player count
            if (_noPlayersText != null)
            {
                if (lobby.currentPlayers == 0)
                {
                    _noPlayersText.gameObject.SetActive(true);
                    _noPlayersText.text = "No players in lobby";
                }
                else
                {
                    _noPlayersText.gameObject.SetActive(true);
                    _noPlayersText.text = ZString.Format("{0} {1} in lobby", 
                        lobby.currentPlayers, 
                        lobby.currentPlayers == 1 ? "player" : "players");
                }
            }
            
            // TODO: Once we have actual player data from the network:
            // - Get NetworkPlayerData list from YargNetworkManager
            // - Instantiate _playerEntryPrefab for each player
            // - Show player name, instrument icon, and ready status
        }
        
        private void ClearPlayerList()
        {
            if (_playerListContainer != null)
            {
                foreach (Transform child in _playerListContainer)
                {
                    Destroy(child.gameObject);
                }
            }
            
            if (_noPlayersText != null)
            {
                _noPlayersText.gameObject.SetActive(false);
            }
        }
        
        private int CalculatePing(YargNetworkManager.LobbyInfo lobby)
        {
            // Calculate ping based on lastSeen timestamp
            long currentTime = System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            long timeSinceLastSeen = currentTime - lobby.lastSeen;
            
            // If we haven't seen the lobby in a while, show high ping
            if (timeSinceLastSeen > 5000)
                return 999;
            
            // Otherwise, simulate based on network discovery interval
            // TODO: Implement proper RTT measurement
            return Random.Range(10, 100);
        }
    }
}
