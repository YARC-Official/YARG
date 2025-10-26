using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Networking;
using System.Collections.Generic;
using YARG.Core;

namespace YARG.Gameplay.HUD
{
    /// <summary>
    /// Displays other players' stats during multiplayer gameplay.
    /// Shows names, scores, combos, and star power status.
    /// </summary>
    public class MultiplayerHUD : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private GameObject playerStatsPrefab;
        
        [SerializeField]
        private Transform playerStatsContainer;
        
        [SerializeField]
        private GameObject multiplayerPanel;
        
        private bool _isMultiplayer;
        private Dictionary<NetworkPlayerData, PlayerStatsDisplay> _playerDisplays = new();
        private float _updateInterval = 0.1f; // Update UI every 100ms
        private float _lastUpdateTime;

        private void Start()
        {
            // Check if we're in multiplayer mode
            if (YargNetworkManager.Instance == null || !YargNetworkManager.Instance.isNetworkActive)
            {
                _isMultiplayer = false;
                if (multiplayerPanel != null)
                {
                    multiplayerPanel.SetActive(false);
                }
                return;
            }

            _isMultiplayer = true;
            if (multiplayerPanel != null)
            {
                multiplayerPanel.SetActive(true);
            }

            Debug.Log("[MultiplayerHUD] Initializing multiplayer HUD");

            // Create displays for all players
            RefreshPlayerList();
        }

        private void Update()
        {
            if (!_isMultiplayer)
                return;

            // Throttle updates
            if (Time.time - _lastUpdateTime < _updateInterval)
                return;

            _lastUpdateTime = Time.time;

            // Update all player displays
            foreach (var kvp in _playerDisplays)
            {
                if (kvp.Key != null && kvp.Value != null)
                {
                    kvp.Value.UpdateDisplay(kvp.Key);
                }
            }
        }

        private void RefreshPlayerList()
        {
            if (YargNetworkManager.Instance == null)
                return;

            // Clear existing displays
            foreach (var display in _playerDisplays.Values)
            {
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }
            _playerDisplays.Clear();

            // Get all connected players
            var players = YargNetworkManager.Instance.GetAllPlayers();
            
            foreach (var playerData in players)
            {
                if (playerData == null)
                    continue;

                // Create display for this player (but not for local player)
                if (!playerData.IsLocalUser)
                {
                    CreatePlayerDisplay(playerData);
                }
            }

            Debug.Log($"[MultiplayerHUD] Created displays for {_playerDisplays.Count} remote players");
        }

        private void CreatePlayerDisplay(NetworkPlayerData playerData)
        {
            if (playerStatsContainer == null)
            {
                Debug.LogWarning("[MultiplayerHUD] playerStatsContainer is null - cannot create player display");
                return;
            }

            GameObject displayObj;
            
            if (playerStatsPrefab != null)
            {
                displayObj = Instantiate(playerStatsPrefab, playerStatsContainer);
            }
            else
            {
                // Create a simple default display if no prefab is assigned
                displayObj = new GameObject($"Player_{playerData.PlayerName}");
                displayObj.transform.SetParent(playerStatsContainer, false);
            }

            var display = displayObj.GetComponent<PlayerStatsDisplay>();
            if (display == null)
            {
                display = displayObj.AddComponent<PlayerStatsDisplay>();
                display.InitializeDefault();
            }

            _playerDisplays[playerData] = display;
            display.UpdateDisplay(playerData);
        }

        private void OnDestroy()
        {
            // Clean up
            foreach (var display in _playerDisplays.Values)
            {
                if (display != null)
                {
                    Destroy(display.gameObject);
                }
            }
            _playerDisplays.Clear();
        }
    }

    /// <summary>
    /// Displays a single player's stats.
    /// Can be used with a prefab or created programmatically.
    /// </summary>
    public class PlayerStatsDisplay : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField]
        private TextMeshProUGUI nameText;
        
        [SerializeField]
        private TextMeshProUGUI scoreText;
        
        [SerializeField]
        private TextMeshProUGUI comboText;
        
        [SerializeField]
        private Image starPowerIndicator;
        
        [SerializeField]
        private Image instrumentIcon;

        /// <summary>
        /// Initialize with default UI elements (used if no prefab provided).
        /// </summary>
        public void InitializeDefault()
        {
            // Create a simple default layout
            var layoutGroup = gameObject.AddComponent<HorizontalLayoutGroup>();
            layoutGroup.spacing = 10;
            layoutGroup.childControlWidth = false;
            layoutGroup.childControlHeight = false;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;

            // Name text
            var nameObj = new GameObject("NameText");
            nameObj.transform.SetParent(transform, false);
            nameText = nameObj.AddComponent<TextMeshProUGUI>();
            nameText.fontSize = 18;
            nameText.color = Color.white;
            nameText.text = "Player";
            nameText.rectTransform.sizeDelta = new Vector2(150, 30);

            // Score text
            var scoreObj = new GameObject("ScoreText");
            scoreObj.transform.SetParent(transform, false);
            scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
            scoreText.fontSize = 16;
            scoreText.color = Color.yellow;
            scoreText.text = "0";
            scoreText.rectTransform.sizeDelta = new Vector2(100, 30);

            // Combo text
            var comboObj = new GameObject("ComboText");
            comboObj.transform.SetParent(transform, false);
            comboText = comboObj.AddComponent<TextMeshProUGUI>();
            comboText.fontSize = 16;
            comboText.color = Color.cyan;
            comboText.text = "0x";
            comboText.rectTransform.sizeDelta = new Vector2(60, 30);

            Debug.Log("[PlayerStatsDisplay] Initialized with default UI");
        }

        public void UpdateDisplay(NetworkPlayerData playerData)
        {
            if (playerData == null)
                return;

            // Update name
            if (nameText != null)
            {
                string displayName = playerData.PlayerName;
                if (playerData.IsHost)
                {
                    displayName += " (Host)";
                }
                nameText.text = displayName;
            }

            // Update score
            if (scoreText != null)
            {
                scoreText.text = playerData.CurrentScore.ToString("N0");
            }

            // Update combo
            if (comboText != null)
            {
                if (playerData.CurrentCombo > 0)
                {
                    comboText.text = $"{playerData.CurrentCombo}x";
                    comboText.color = Color.cyan;
                }
                else
                {
                    comboText.text = "0x";
                    comboText.color = Color.gray;
                }
            }

            // Update star power indicator
            if (starPowerIndicator != null)
            {
                starPowerIndicator.gameObject.SetActive(playerData.IsStarPowerActive);
                
                // Optional: Set color based on star power amount
                if (playerData.IsStarPowerActive)
                {
                    starPowerIndicator.color = Color.yellow;
                }
                else
                {
                    // Show charge level when not active
                    float alpha = playerData.StarPowerAmount;
                    starPowerIndicator.color = new Color(1f, 1f, 0f, alpha * 0.5f);
                }

                if (starPowerIndicator.type == Image.Type.Filled)
                {
                    starPowerIndicator.fillAmount = Mathf.Clamp01(playerData.StarPowerAmount);
                }
            }

            // Update instrument icon
            if (instrumentIcon != null)
            {
                // You can set instrument-specific sprites here
                // For now, just enable/disable based on instrument value
                instrumentIcon.gameObject.SetActive(playerData.Instrument >= 0);
            }
        }
    }
}
