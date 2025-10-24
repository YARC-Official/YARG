using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Networking;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// UI representation of a player in the lobby.
    /// Shows player name, ping, instrument, and kick button (for host).
    /// </summary>
    public class PlayerView : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TextMeshProUGUI playerNameText;
        [SerializeField] private TextMeshProUGUI pingText;
        [SerializeField] private TextMeshProUGUI instrumentText;
        [SerializeField] private Button kickButton;
        [SerializeField] private GameObject hostBadge; // Optional: visual indicator for host
        [SerializeField] private GameObject playerIcon; // Optional: visual indicator for regular players (inverse of hostBadge)
        
        [Header("Selection Visuals")]
        [SerializeField] private GameObject normalBackground;
        [SerializeField] private GameObject selectedBackground;
        [SerializeField] private Image highlightImage; // Optional: for highlighting on hover/selection

        private NetworkPlayerData _playerData;
        private bool _isLocalPlayer;
        private bool _isHost;
        private bool _isSelected = false;

        public void Initialize(NetworkPlayerData playerData, bool isLocalPlayer, bool viewerIsHost)
        {
            _playerData = playerData;
            _isLocalPlayer = isLocalPlayer;
            // Use the synced IsHost property from NetworkPlayerData
            _isHost = playerData.IsHost;
            
            UpdateDisplay();
            
            // Show kick button only if viewer is host and this is not the local player
            if (kickButton != null)
            {
                bool canKick = viewerIsHost && !isLocalPlayer && !_isHost;
                kickButton.gameObject.SetActive(canKick);
                
                if (canKick)
                {
                    kickButton.onClick.AddListener(OnKickClicked);
                }
            }
            
            // Show host badge if this player is the host
            if (hostBadge != null)
            {
                hostBadge.SetActive(_isHost);
            }
            
            // Show player icon if this player is NOT the host (inverse of host badge)
            if (playerIcon != null)
            {
                playerIcon.SetActive(!_isHost);
            }
            
            // Subscribe to player data changes
            if (_playerData != null)
            {
                _playerData.OnPlayerNameChangedEvent += OnPlayerNameChanged;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_playerData != null)
            {
                _playerData.OnPlayerNameChangedEvent -= OnPlayerNameChanged;
            }
            
            if (kickButton != null)
            {
                kickButton.onClick.RemoveListener(OnKickClicked);
            }
        }

        private void Update()
        {
            // Update ping every frame (or throttle if needed)
            UpdatePing();
        }

        private void UpdateDisplay()
        {
            if (_playerData == null) return;
            
            // Update player name
            if (playerNameText != null)
            {
                string displayName = _playerData.PlayerName;
                if (_isHost) displayName += " (Host)";
                if (_isLocalPlayer) displayName += " (You)";
                playerNameText.text = displayName;
            }
            
            // Update instrument
            UpdateInstrument();
        }

        private void UpdatePing()
        {
            if (pingText == null || _playerData == null) return;
            
            // Only the host shows 0ms (they have no ping to themselves)
            if (_isHost)
            {
                pingText.text = "0ms";
                return;
            }
            
            // All other players (including local non-host) show their ping to the server
            float ping = _playerData.Ping;
            if (ping > 0)
            {
                pingText.text = $"{Mathf.RoundToInt(ping)}ms";
            }
            else
            {
                pingText.text = "?ms";
            }
        }

        private void UpdateInstrument()
        {
            if (instrumentText == null || _playerData == null) return;
            
            // TODO: Get actual instrument from player profile/selection
            // For now, show placeholder
            instrumentText.text = "Guitar"; // Placeholder
            
            // Future implementation:
            // var profile = _playerData.GetPlayerProfile();
            // if (profile != null)
            // {
            //     instrumentText.text = profile.CurrentInstrument.ToString();
            // }
        }

        private void OnPlayerNameChanged(string newName)
        {
            UpdateDisplay();
        }

        private void OnKickClicked()
        {
            if (_playerData == null || _playerData.connectionToClient == null)
            {
                Debug.LogWarning("[PlayerView] Cannot kick player - no connection data");
                return;
            }
            
            Debug.Log($"[PlayerView] Kicking player: {_playerData.PlayerName}");
            
            // Call kick method on network manager
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.KickPlayer(_playerData.connectionToClient);
            }
        }

        /// <summary>
        /// Manually update the view (call if player data changes outside of events)
        /// </summary>
        public void Refresh()
        {
            UpdateDisplay();
        }
        
        /// <summary>
        /// Set the selection state of this player view
        /// </summary>
        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            UpdateSelectionVisual();
        }
        
        private void UpdateSelectionVisual()
        {
            // Toggle background visibility
            if (normalBackground != null)
            {
                normalBackground.SetActive(!_isSelected);
            }
            
            if (selectedBackground != null)
            {
                selectedBackground.SetActive(_isSelected);
            }
            
            // Optional: Update highlight image color/alpha
            if (highlightImage != null)
            {
                var color = highlightImage.color;
                color.a = _isSelected ? 0.3f : 0f;
                highlightImage.color = color;
            }
        }
    }
}
