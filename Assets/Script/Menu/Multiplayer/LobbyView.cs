using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.ListMenu;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// View component for displaying lobby information in the browser list.
    /// Follows YARG's ViewObject pattern.
    /// </summary>
    public class LobbyView : ViewObject<LobbyViewType>
    {
        [Header("Lobby Info Display")]
        [SerializeField]
        private TextMeshProUGUI _playerCountText;
        [SerializeField]
        private TextMeshProUGUI _pingText;
        [SerializeField]
        private GameObject _passwordIcon;
        
        [Header("Favorite Button")]
        [SerializeField]
        private GameObject _favoriteButtonContainer;
        [SerializeField]
        private GameObject _favoriteButtonContainerSelected;
        [SerializeField]
        private Image[] _favoriteButtons;
        
        [Header("Sprites")]
        [SerializeField]
        private Sprite _favoriteUnfilled;
        [SerializeField]
        private Sprite _favoriteFilled;
        
        public override void Show(bool selected, LobbyViewType viewType)
        {
            base.Show(selected, viewType);
            
            // Check if this is a lobby entry (not a category)
            bool isLobbyEntry = viewType is DiscoveredLobbyViewType;
            
            if (isLobbyEntry)
            {
                var lobbyView = (DiscoveredLobbyViewType)viewType;
                
                // Set player count
                if (_playerCountText != null)
                {
                    _playerCountText.text = lobbyView.GetPlayerCountText();
                }
                
                // Set ping
                if (_pingText != null)
                {
                    _pingText.text = lobbyView.GetPingText();
                }
                
                // Show/hide password icon
                if (_passwordIcon != null)
                {
                    _passwordIcon.SetActive(lobbyView.HasPassword());
                }
                
                // Show/hide favorite button
                bool showFavorite = viewType.ShowFavoriteButton;
                if (_favoriteButtonContainer != null)
                {
                    _favoriteButtonContainer.SetActive(!selected && showFavorite);
                }
                if (_favoriteButtonContainerSelected != null)
                {
                    _favoriteButtonContainerSelected.SetActive(selected && showFavorite);
                }
                
                // Update favorite sprite
                UpdateFavoriteSprite(viewType.IsFavorited);
            }
            else
            {
                // This is a category header, hide lobby-specific elements
                if (_playerCountText != null) _playerCountText.text = "";
                if (_pingText != null) _pingText.text = "";
                if (_passwordIcon != null) _passwordIcon.SetActive(false);
                if (_favoriteButtonContainer != null) _favoriteButtonContainer.SetActive(false);
                if (_favoriteButtonContainerSelected != null) _favoriteButtonContainerSelected.SetActive(false);
            }
        }
        
        private void UpdateFavoriteSprite(bool isFavorited)
        {
            if (_favoriteButtons == null) return;
            
            foreach (var button in _favoriteButtons)
            {
                if (button != null)
                {
                    button.sprite = isFavorited ? _favoriteFilled : _favoriteUnfilled;
                }
            }
        }
        
        /// <summary>
        /// Called when the join button is clicked (or main action is triggered).
        /// </summary>
        public void JoinClick()
        {
            if (!Showing) return;
            
            ViewType.OnJoinClick();
        }
        
        /// <summary>
        /// Called when the favorite button is clicked.
        /// </summary>
        public void FavoriteClick()
        {
            if (!Showing) return;
            
            ViewType.OnFavoriteClick();
            
            // Update the sprite after in case the state changed
            UpdateFavoriteSprite(ViewType.IsFavorited);
        }
    }
}
