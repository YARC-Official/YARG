using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;
using YARG.Core.Input;
using YARG.Menu.ListMenu;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Networking;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Lobby browser menu using YARG's ListMenu pattern.
    /// Shows discovered lobbies with favorites support.
    /// </summary>
    public class LobbyBrowserMenu : ListMenu<LobbyViewType, LobbyView>
    {
        [Header("UI References")]
        [SerializeField]
        private TextMeshProUGUI _statusText;
        [SerializeField]
        private LobbyBrowserSidebar _sidebar;
        
        private LobbyFavorites _favorites;
        private List<YargNetworkManager.LobbyInfo> _currentLobbies = new List<YargNetworkManager.LobbyInfo>();
        private YargNetworkManager.LobbyInfo _selectedLobby;
        
        protected override int ExtraListViewPadding => 15;
        
        private void Awake()
        {
            _favorites = new LobbyFavorites();
            _favorites.OnFavoritesChanged += RefreshList;
        }
        
        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Up, "Menu.Common.Up",
                    ctx =>
                    {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex--;
                    }),
                new NavigationScheme.Entry(MenuAction.Down, "Menu.Common.Down",
                    ctx =>
                    {
                        SetWrapAroundState(!ctx.IsRepeat);
                        SelectedIndex++;
                    }),
                new NavigationScheme.Entry(MenuAction.Green, "Menu.Common.Confirm",
                    () => CurrentSelection?.OnJoinClick()),
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                new NavigationScheme.Entry(MenuAction.Yellow, "Menu.MusicLibrary.AddToFavorites",
                    () => CurrentSelection?.OnFavoriteClick()),
                new NavigationScheme.Entry(MenuAction.Blue, "Menu.Common.Refresh",
                    RefreshLobbies)
            }, false));
            
            // Subscribe to network events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyListUpdated += OnLobbyListUpdated;
            }
            
            // Initialize sidebar
            if (_sidebar != null)
            {
                _sidebar.Initialize(this);
            }
            
            // Start refreshing
            RefreshLobbies();
        }
        
        private void OnDisable()
        {
            Navigator.Instance?.PopScheme();
            
            // Unsubscribe from events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyListUpdated -= OnLobbyListUpdated;
            }
        }
        
        private void Update()
        {
            // Update sidebar with current selection
            if (_sidebar != null && CurrentSelection is DiscoveredLobbyViewType lobbyViewType)
            {
                if (_selectedLobby != lobbyViewType.LobbyInfo)
                {
                    _selectedLobby = lobbyViewType.LobbyInfo;
                    _sidebar.SetLobby(_selectedLobby);
                }
            }
            else if (_sidebar != null && _selectedLobby != null)
            {
                _selectedLobby = null;
                _sidebar.ClearLobby();
            }
        }
        
        protected override List<LobbyViewType> CreateViewList()
        {
            var viewTypes = new List<LobbyViewType>();
            
            if (_currentLobbies.Count == 0)
            {
                return viewTypes;
            }
            
            // Separate favorites and non-favorites
            var favorites = _currentLobbies
                .Where(lobby => _favorites.IsFavorited(lobby.ipAddress))
                .OrderByDescending(lobby => lobby.currentPlayers)
                .ToList();
            
            var others = _currentLobbies
                .Where(lobby => !_favorites.IsFavorited(lobby.ipAddress))
                .OrderByDescending(lobby => lobby.currentPlayers)
                .ToList();
            
            // Add favorites section
            if (favorites.Count > 0)
            {
                viewTypes.Add(new LobbyCategoryViewType("★ FAVORITE LOBBIES"));
                foreach (var lobby in favorites)
                {
                    viewTypes.Add(new DiscoveredLobbyViewType(lobby, this, _favorites));
                }
            }
            
            // Add all lobbies section
            if (others.Count > 0)
            {
                string header = favorites.Count > 0 ? "ALL LOBBIES" : "AVAILABLE LOBBIES";
                viewTypes.Add(new LobbyCategoryViewType(header));
                foreach (var lobby in others)
                {
                    viewTypes.Add(new DiscoveredLobbyViewType(lobby, this, _favorites));
                }
            }
            
            return viewTypes;
        }
        
        private void RefreshList()
        {
            RequestViewListUpdate();
        }
        
        public void RefreshLobbies()
        {
            if (_statusText != null)
            {
                _statusText.text = "Searching for lobbies...";
            }
            
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.RefreshLobbyList();
            }
        }
        
        private void OnLobbyListUpdated(List<YargNetworkManager.LobbyInfo> lobbies)
        {
            _currentLobbies = lobbies;
            
            // Update status text
            if (_statusText != null)
            {
                if (lobbies.Count == 0)
                {
                    _statusText.text = "No lobbies found";
                }
                else
                {
                    int favoriteCount = lobbies.Count(l => _favorites.IsFavorited(l.ipAddress));
                    if (favoriteCount > 0)
                    {
                        _statusText.text = $"{lobbies.Count} {(lobbies.Count == 1 ? "lobby" : "lobbies")} found ({favoriteCount} favorite{(favoriteCount == 1 ? "" : "s")})";
                    }
                    else
                    {
                        _statusText.text = $"{lobbies.Count} {(lobbies.Count == 1 ? "lobby" : "lobbies")} found";
                    }
                }
            }
            
            RefreshList();
        }
        
        /// <summary>
        /// Called by LobbyViewType to join a lobby.
        /// </summary>
        public void JoinLobby(YargNetworkManager.LobbyInfo lobby)
        {
            if (lobby.hasPassword)
            {
                // Show password dialog
                ShowPasswordDialog(lobby);
            }
            else
            {
                // Join directly
                JoinLobbyWithPassword(lobby, string.Empty);
            }
        }
        
        private void ShowPasswordDialog(YargNetworkManager.LobbyInfo lobby)
        {
            // TODO: Use YARG's DialogManager to show input dialog
            // For now, just try to join with empty password (will fail)
            Debug.LogWarning($"Lobby '{lobby.lobbyName}' requires a password. Password dialog not yet implemented.");
            
            // Temporary: Show error message
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ShowMessage(
                    "Password Required",
                    $"The lobby '{lobby.lobbyName}' requires a password.\nPassword input dialog coming soon!"
                );
            }
        }
        
        private void JoinLobbyWithPassword(YargNetworkManager.LobbyInfo lobby, string password)
        {
            if (YargNetworkManager.Instance != null)
            {
                // Update last connected time for favorites
                if (_favorites.IsFavorited(lobby.ipAddress))
                {
                    _favorites.UpdateLastConnected(lobby.ipAddress);
                }
                
                YargNetworkManager.Instance.JoinDiscoveredLobby(lobby, password);
            }
        }
        
        private void Back()
        {
            MenuManager.Instance.PopMenu();
        }
    }
}
