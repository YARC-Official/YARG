using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.UI;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Networking;

namespace YARG.Menu.DifficultySelect
{
    /// <summary>
    /// UI component for displaying a single player's status in the multiplayer difficulty select screen
    /// </summary>
    public class MultiplayerPlayerEntry : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI _iconsText; // Combined instrument + difficulty
        [SerializeField] private TextMeshProUGUI _playerNameText;
        [SerializeField] private Image _readyStatusIcon;
        
        private NetworkPlayerData _playerData;
        private AsyncOperationHandle<Sprite> _readyIconHandle;
        
        public void Initialize(NetworkPlayerData playerData)
        {
            _playerData = playerData;
            
            // Debug: Check if references are assigned
            Debug.Log($"[MultiplayerPlayerEntry] Initialize called for {playerData?.PlayerName}");
            Debug.Log($"[MultiplayerPlayerEntry] References - Icons: {_iconsText != null}, Name: {_playerNameText != null}, Status: {_readyStatusIcon != null}");
            
            // Subscribe to player events
            if (_playerData != null)
            {
                _playerData.OnReadyStateChangedEvent += OnReadyStateChanged;
                _playerData.OnInstrumentChangedEvent += OnInstrumentChanged;
                _playerData.OnDifficultyChangedEvent += OnDifficultyChanged;
            }
            
            UpdateDisplay();
        }
        
        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_playerData != null)
            {
                _playerData.OnReadyStateChangedEvent -= OnReadyStateChanged;
                _playerData.OnInstrumentChangedEvent -= OnInstrumentChanged;
                _playerData.OnDifficultyChangedEvent -= OnDifficultyChanged;
            }
            
            // Release addressable handles
            if (_readyIconHandle.IsValid())
            {
                Addressables.Release(_readyIconHandle);
            }
        }
        
        private void OnReadyStateChanged(bool isReady)
        {
            Debug.Log($"[MultiplayerPlayerEntry] OnReadyStateChanged called - Player: {_playerData?.PlayerName}, IsReady: {isReady}");
            UpdateReadyStatus();
        }
        
        private void OnInstrumentChanged(int newInstrument, int newDifficulty)
        {
            Debug.Log($"[MultiplayerPlayerEntry] OnInstrumentChanged called - Player: {_playerData?.PlayerName}, Instrument: {newInstrument}, Difficulty: {newDifficulty}");
            UpdatePlayerNameWithIcons();
        }
        
        private void OnDifficultyChanged(int newInstrument, int newDifficulty)
        {
            Debug.Log($"[MultiplayerPlayerEntry] OnDifficultyChanged called - Player: {_playerData?.PlayerName}, Instrument: {newInstrument}, Difficulty: {newDifficulty}");
            UpdatePlayerNameWithIcons();
        }
        
        private void UpdateDisplay()
        {
            if (_playerData == null) return;
            
            UpdatePlayerNameWithIcons();
            UpdateReadyStatus();
        }
        
        private void UpdatePlayerNameWithIcons()
        {
            if (_playerNameText == null || _playerData == null)
            {
                Debug.LogWarning($"[MultiplayerPlayerEntry] Cannot update name - _playerNameText: {_playerNameText != null}, _playerData: {_playerData != null}");
                return;
            }
            
            // Get instrument sprite name
            Instrument instrument = (Instrument)_playerData.Instrument;
            string instrumentSprite = instrument.ToResourceName();
            
            // Get difficulty sprite name - map to proper sprite names
            Difficulty difficulty = (Difficulty)_playerData.Difficulty;
            string difficultyName = difficulty switch
            {
                Difficulty.Beginner   => "Easy",    // Beginner uses Easy sprite
                Difficulty.Easy       => "Easy",
                Difficulty.Medium     => "Medium",
                Difficulty.Hard       => "Hard",
                Difficulty.Expert     => "Expert",
                Difficulty.ExpertPlus => "ExpertPlus",
                _ => "Easy"
            };
            
            // Update combined icons (instrument + difficulty)
            if (_iconsText != null)
            {
                _iconsText.text = $"<sprite name=\"{instrumentSprite}\"><sprite name=\"{difficultyName}\">";
            }
            
            // Update player name with ellipsis overflow
            string playerName = _playerData.PlayerName;
            _playerNameText.text = playerName;
            _playerNameText.enableWordWrapping = false;
            _playerNameText.overflowMode = TextOverflowModes.Ellipsis;
            _playerNameText.horizontalAlignment = HorizontalAlignmentOptions.Left;
            
            Debug.Log($"[MultiplayerPlayerEntry] Updated - Player: {playerName}, Instrument: {instrumentSprite}, Difficulty: {difficultyName}");
        }
        
        private void UpdateReadyStatus()
        {
            if (_readyStatusIcon == null || _playerData == null)
            {
                Debug.LogWarning($"[MultiplayerPlayerEntry] UpdateReadyStatus - _readyStatusIcon: {_readyStatusIcon != null}, _playerData: {_playerData != null}");
                return;
            }
            
            Debug.Log($"[MultiplayerPlayerEntry] UpdateReadyStatus - Player: {_playerData.PlayerName}, IsReady: {_playerData.IsReady}");
            
            // Release previous handle if valid
            if (_readyIconHandle.IsValid())
            {
                Addressables.Release(_readyIconHandle);
            }
            
            // Load appropriate icon and set color based on ready state
            string iconPath;
            Color iconColor;
            
            if (_playerData.IsReady)
            {
                // Ready: Green checkmark
                iconPath = "AssortedIcons[AssortedIcons_0]"; // Checkmark icon
                iconColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Bright green
            }
            else
            {
                // Not ready: Red X
                iconPath = "CloseIcon"; // X icon
                iconColor = new Color(0.9f, 0.2f, 0.2f, 1f); // Bright red
            }
            
            Debug.Log($"[MultiplayerPlayerEntry] Loading ready icon from: {iconPath}");
            
            _readyIconHandle = Addressables.LoadAssetAsync<Sprite>(iconPath);
            _readyIconHandle.Completed += handle =>
            {
                if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
                {
                    if (_readyStatusIcon != null)
                    {
                        _readyStatusIcon.sprite = handle.Result;
                        _readyStatusIcon.color = iconColor;
                        _readyStatusIcon.enabled = true;
                        Debug.Log($"[MultiplayerPlayerEntry] Successfully loaded ready icon: {iconPath} with color {iconColor}");
                    }
                }
                else
                {
                    Debug.LogWarning($"[MultiplayerPlayerEntry] Failed to load ready icon: {iconPath}, Status: {handle.Status}");
                    if (_readyStatusIcon != null)
                    {
                        _readyStatusIcon.enabled = false;
                    }
                }
            };
        }
        
        /// <summary>
        /// Manual update method for force-refreshing the display
        /// </summary>
        public void RefreshDisplay()
        {
            UpdateDisplay();
        }

        /// <summary>
        /// Set player name and instrument icon manually (for sidebar)
        /// </summary>
        public void SetPlayer(string name, string instrument)
        {
            if (_playerNameText != null)
                _playerNameText.text = name;
            if (_iconsText != null)
                _iconsText.text = instrument;
        }
    }
}
