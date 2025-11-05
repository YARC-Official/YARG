using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using TMPro;
using YARG.Core;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Data;
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
        [SerializeField] private ColoredButton kickButton;
        [SerializeField] private GameObject hostBadge; // Optional: visual indicator for host
        [SerializeField] private Image hostBadgeImage;
        [SerializeField] private GameObject playerIcon; // Optional: visual indicator for regular players (inverse of hostBadge)
        [SerializeField] private Image playerIconImage;
        [SerializeField] private Image pingIcon;
        
        [Header("Selection Visuals")]
        [SerializeField] private GameObject normalBackground;
        [SerializeField] private GameObject selectedBackground;
        [SerializeField] private Image highlightImage; // Optional: for highlighting on hover/selection

        private NetworkPlayerData _playerData;
        private bool _isLocalPlayer;
        private bool _isHost;
        private bool _isSelected = false;

        private Color _defaultPingTextColor;
        private bool _hasCachedPingTextColor;
        private Color _hostBadgeDefaultColor;
        private bool _hasCachedHostBadgeColor;
        private string _currentInstrumentIconKey;

        private static readonly Color PING_GOOD_COLOR = new(0.3f, 1f, 0.3f);
        private static readonly Color PING_AVERAGE_COLOR = new(1f, 1f, 0.3f);
        private static readonly Color PING_POOR_COLOR = new(1f, 0.3f, 0.3f);
        private static readonly Color PING_ZERO_COLOR = Color.white;
        private const float PING_GOOD_THRESHOLD = 50f;
        private const float PING_AVERAGE_THRESHOLD = 100f;

        public void Initialize(NetworkPlayerData playerData, bool isLocalPlayer, bool viewerIsHost)
        {
            _playerData = playerData;
            _isLocalPlayer = isLocalPlayer;
            // Use the synced IsHost property from NetworkPlayerData
            _isHost = playerData.IsHost;
            
            if (pingText != null && !_hasCachedPingTextColor)
            {
                _defaultPingTextColor = pingText.color;
                _hasCachedPingTextColor = true;
            }

            if (hostBadgeImage != null && !_hasCachedHostBadgeColor)
            {
                _hostBadgeDefaultColor = hostBadgeImage.color;
                _hasCachedHostBadgeColor = true;
            }

            UpdateDisplay();
            
            // Show kick button only if viewer is host and this is not the local player
            if (kickButton != null)
            {
                kickButton.OnClick.RemoveListener(OnKickClicked);
                bool canKick = viewerIsHost && !isLocalPlayer && !_isHost;
                kickButton.gameObject.SetActive(canKick);
                
                if (canKick)
                {
                    MenuColors colors = MenuData.Instance != null ? MenuData.Colors : null;
                    if (colors != null)
                    {
                        kickButton.SetBackgroundAndTextColor(colors.CancelButton);
                    }

                    kickButton.OnClick.AddListener(OnKickClicked);
                }
            }
            
            UpdateHostVisuals();
            
            // Subscribe to player data changes
            if (_playerData != null)
            {
                _playerData.OnPlayerNameChangedEvent += OnPlayerNameChanged;
                _playerData.OnInstrumentChangedEvent += OnInstrumentOrDifficultyChanged;
                _playerData.OnDifficultyChangedEvent += OnInstrumentOrDifficultyChanged;
            }
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (_playerData != null)
            {
                _playerData.OnPlayerNameChangedEvent -= OnPlayerNameChanged;
                _playerData.OnInstrumentChangedEvent -= OnInstrumentOrDifficultyChanged;
                _playerData.OnDifficultyChangedEvent -= OnInstrumentOrDifficultyChanged;
            }
            
            if (kickButton != null)
            {
                kickButton.OnClick.RemoveListener(OnKickClicked);
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
                if (_isLocalPlayer) displayName += " (You)";
                playerNameText.text = displayName;
            }

            UpdateHostVisuals();
            
            // Update instrument
            UpdateInstrument();
        }

        private void UpdatePing()
        {
            if (pingText == null || _playerData == null) return;
            
            float? pingValue = null;

            // Only the host shows 0ms (they have no ping to themselves)
            if (_isHost)
            {
                pingText.text = "0ms";
                pingValue = 0f;
            }
            else
            {
                // All other players (including local non-host) show their ping to the server
                float ping = _playerData.Ping;
                if (ping > 0f)
                {
                    pingValue = ping;
                    pingText.text = $"{Mathf.RoundToInt(ping)}ms";
                }
                else
                {
                    pingText.text = "?ms";
                }
            }

            UpdatePingColor(pingValue);
        }

        private void UpdateInstrument()
        {
            if (_playerData == null)
            {
                return;
            }

            int instrumentIndex = _playerData.Instrument;
            if (instrumentIndex < 0)
            {
                UpdateInstrumentIcon(null);
                return;
            }

            try
            {
                var instrument = (Instrument)instrumentIndex;
                UpdateInstrumentIcon(instrument);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] Failed to interpret instrument value '{instrumentIndex}': {ex.Message}");
                UpdateInstrumentIcon(null);
            }
        }

        private void OnPlayerNameChanged(string newName)
        {
            UpdateDisplay();
        }

        private void OnInstrumentOrDifficultyChanged(int newInstrument, int newDifficulty)
        {
            UpdateInstrument();
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
            UpdatePing();
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

        private void UpdateHostVisuals()
        {
            if (hostBadge != null)
            {
                if (!hostBadge.activeSelf)
                {
                    hostBadge.SetActive(true);
                }

                var badgeImage = hostBadgeImage != null ? hostBadgeImage : hostBadge.GetComponent<Image>();
                if (badgeImage != null)
                {
                    if (!_hasCachedHostBadgeColor)
                    {
                        _hostBadgeDefaultColor = badgeImage.color;
                        _hasCachedHostBadgeColor = true;
                    }

                    badgeImage.enabled = _isHost;
                    badgeImage.raycastTarget = _isHost;

                    if (_isHost && _hasCachedHostBadgeColor)
                    {
                        badgeImage.color = _hostBadgeDefaultColor;
                    }
                }
            }

            if (playerIcon != null && !playerIcon.activeSelf)
            {
                playerIcon.SetActive(true);
            }
        }

        private void UpdatePingColor(float? ping)
        {
            if (pingText == null) return;

            if (!_hasCachedPingTextColor)
            {
                _defaultPingTextColor = pingText.color;
                _hasCachedPingTextColor = true;
            }

            Color fallback = _hasCachedPingTextColor ? _defaultPingTextColor : pingText.color;
            Color targetColor = fallback;

            if (ping.HasValue)
            {
                float value = Mathf.Max(0f, ping.Value);

                if (value <= Mathf.Epsilon)
                {
                    targetColor = PING_ZERO_COLOR;
                }
                else if (value < PING_GOOD_THRESHOLD)
                {
                    targetColor = PING_GOOD_COLOR;
                }
                else if (value < PING_AVERAGE_THRESHOLD)
                {
                    targetColor = PING_AVERAGE_COLOR;
                }
                else
                {
                    targetColor = PING_POOR_COLOR;
                }
            }

            pingText.color = targetColor;

            if (pingIcon != null)
            {
                pingIcon.color = targetColor;
                if (!pingIcon.gameObject.activeSelf)
                {
                    pingIcon.gameObject.SetActive(true);
                }
            }
        }

        private void UpdateInstrumentIcon(Instrument? instrument)
        {
            if (playerIcon != null && !playerIcon.activeSelf)
            {
                playerIcon.SetActive(true);
            }

            if (playerIconImage == null)
            {
                return;
            }

            if (!instrument.HasValue)
            {
                playerIconImage.enabled = false;
                playerIconImage.sprite = null;
                _currentInstrumentIconKey = null;
                return;
            }

            string resourceName;
            try
            {
                resourceName = instrument.Value.ToResourceName();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] Failed to resolve resource name for instrument '{instrument}': {ex.Message}");
                resourceName = null;
            }

            if (string.IsNullOrEmpty(resourceName))
            {
                var instrumentValue = instrument.Value;
                resourceName = instrumentValue switch
                {
                    Instrument.SixFretBass => "bass",
                    Instrument.SixFretGuitar or Instrument.SixFretRhythm or Instrument.SixFretCoopGuitar => "guitar",
                    Instrument.FourLaneDrums or Instrument.ProDrums or Instrument.FiveLaneDrums or Instrument.EliteDrums => "drums",
                    Instrument.ProGuitar_17Fret or Instrument.ProGuitar_22Fret or Instrument.ProBass_17Fret or Instrument.ProBass_22Fret => "realGuitar",
                    Instrument.ProKeys => "realKeys",
                    Instrument.Vocals or Instrument.Harmony => "vocals",
                    Instrument.Band => "guitar",
                    _ => "guitar"
                };
            }

            string address = $"InstrumentIcons[{resourceName}]";
            if (_currentInstrumentIconKey == address && playerIconImage.sprite != null)
            {
                playerIconImage.enabled = true;
                return;
            }

            try
            {
                var sprite = Addressables.LoadAssetAsync<Sprite>(address).WaitForCompletion();
                playerIconImage.sprite = sprite;
                playerIconImage.enabled = sprite != null;
                _currentInstrumentIconKey = sprite != null ? address : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[PlayerView] Failed to load instrument icon '{address}': {ex.Message}");
                playerIconImage.enabled = false;
                playerIconImage.sprite = null;
                _currentInstrumentIconKey = null;
            }
        }
    }
}
