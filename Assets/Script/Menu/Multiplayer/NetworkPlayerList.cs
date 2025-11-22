using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using YARG.Networking;
using YARG.Menu.Persistent;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Manages the player list UI for networked multiplayer sessions.
    /// Shows connected players with their ready status.
    /// </summary>
    public class NetworkPlayerList : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private GameObject playerEntryPrefab;
        [SerializeField] private TextMeshProUGUI playerCountText;
        
        [Header("Host Controls")]
        [SerializeField] private GameObject hostControlsPanel;
        [SerializeField] private Button startGameButton;
        [SerializeField] private TextMeshProUGUI startGameButtonText;

        private Dictionary<string, GameObject> _playerEntries = new Dictionary<string, GameObject>();
        private bool _isHost = false;

        private void Start()
        {
            // Subscribe to network events
            if (YargNetworkManager.Instance != null)
            {
                // TODO: Subscribe to player joined/left events when implemented
                YargNetworkManager.Instance.OnLobbyLeft += OnLobbyLeft;
            }

            RefreshPlayerList();
        }

        private void OnDestroy()
        {
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyLeft -= OnLobbyLeft;
            }
        }

        private void OnEnable()
        {
            RefreshPlayerList();
        }

        /// <summary>
        /// Refresh the entire player list from network manager.
        /// </summary>
        public void RefreshPlayerList()
        {
            if (YargNetworkManager.Instance == null || YargNetworkManager.Instance.CurrentLobby == null)
            {
                // Not in a lobby, hide player list
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            _isHost = YargNetworkManager.Instance != null && YargNetworkManager.Instance.LocalUserIsHost();

            // Update host controls visibility
            if (hostControlsPanel != null)
                hostControlsPanel.SetActive(_isHost);

            // Update player count
            var lobby = YargNetworkManager.Instance.CurrentLobby;
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {lobby.currentPlayers}/{lobby.maxPlayers}";
            }

            // TODO: Get actual player list from YargNetworkManager
            // For now, show placeholder based on current players count
            ClearPlayerEntries();
            
            // Create entry for host (always present)
            CreatePlayerEntry(lobby.hostName, true, false);
            
            // Create entries for other players (placeholders for now)
            for (int i = 1; i < lobby.currentPlayers; i++)
            {
                CreatePlayerEntry($"Player {i + 1}", false, false);
            }
        }

        private void ClearPlayerEntries()
        {
            foreach (var entry in _playerEntries.Values)
            {
                Destroy(entry);
            }
            _playerEntries.Clear();
        }

        private void CreatePlayerEntry(string playerName, bool isHost, bool isReady)
        {
            if (playerEntryPrefab == null || playerListContainer == null)
            {
                Debug.LogWarning("PlayerEntry prefab or container not assigned!");
                return;
            }

            var entry = Instantiate(playerEntryPrefab, playerListContainer);
            
            // Set player name
            var nameText = entry.transform.Find("PlayerName")?.GetComponent<TextMeshProUGUI>();
            if (nameText != null)
            {
                nameText.text = playerName;
                if (isHost) nameText.text += " (Host)";
            }

            // Set ready status
            var readyText = entry.transform.Find("ReadyStatus")?.GetComponent<TextMeshProUGUI>();
            if (readyText != null && !isHost)
            {
                readyText.text = isReady ? "✓ Ready" : "Waiting...";
                readyText.color = isReady ? Color.green : Color.yellow;
            }
            else if (readyText != null && isHost)
            {
                readyText.text = "";
            }

            // Set kick button (only visible to host for non-host players)
            var kickButton = entry.transform.Find("KickButton")?.GetComponent<Button>();
            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(_isHost && !isHost);
                if (_isHost && !isHost)
                {
                    string capturedName = playerName;
                    kickButton.onClick.AddListener(() => OnKickPlayer(capturedName));
                }
            }

            _playerEntries[playerName] = entry;
        }

        /// <summary>
        /// Update a specific player's ready status.
        /// </summary>
        public void UpdatePlayerReady(string playerName, bool isReady)
        {
            if (!_playerEntries.TryGetValue(playerName, out var entry)) return;

            var readyText = entry.transform.Find("ReadyStatus")?.GetComponent<TextMeshProUGUI>();
            if (readyText != null)
            {
                readyText.text = isReady ? "✓ Ready" : "Waiting...";
                readyText.color = isReady ? Color.green : Color.yellow;
            }

            UpdateStartButtonState();
        }

        /// <summary>
        /// Add a new player to the list.
        /// </summary>
        public void OnPlayerJoined(string playerName)
        {
            if (_playerEntries.ContainsKey(playerName)) return;
            
            CreatePlayerEntry(playerName, false, false);
            
            var lobby = YargNetworkManager.Instance?.CurrentLobby;
            if (lobby != null && playerCountText != null)
            {
                playerCountText.text = $"Players: {lobby.currentPlayers}/{lobby.maxPlayers}";
            }
        }

        /// <summary>
        /// Remove a player from the list.
        /// </summary>
        public void OnPlayerLeft(string playerName)
        {
            if (_playerEntries.TryGetValue(playerName, out var entry))
            {
                Destroy(entry);
                _playerEntries.Remove(playerName);
                
                var lobby = YargNetworkManager.Instance?.CurrentLobby;
                if (lobby != null && playerCountText != null)
                {
                    playerCountText.text = $"Players: {lobby.currentPlayers}/{lobby.maxPlayers}";
                }
            }
        }

        private void OnKickPlayer(string playerName)
        {
            if (!_isHost) return;

            // TODO: Implement kick functionality in YargNetworkManager
            Debug.Log($"Kicking player: {playerName}");
            
            // For now, just show a message
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ShowMessage("Not Implemented", 
                    $"Kick player '{playerName}' will be implemented when player tracking is added.");
            }
        }

        private void UpdateStartButtonState()
        {
            if (!_isHost || startGameButton == null) return;

            // TODO: Check if all players are ready
            // For now, always enable
            startGameButton.interactable = true;
            
            if (startGameButtonText != null)
            {
                startGameButtonText.text = "Start Game";
            }
        }

        public void OnStartGameClicked()
        {
            if (!_isHost) return;

            // TODO: Implement game start in YargNetworkManager
            Debug.Log("Host starting game...");
            
            if (DialogManager.Instance != null)
            {
                DialogManager.Instance.ShowMessage("Not Implemented",
                    "Starting the networked game will be implemented in the next phase.");
            }
        }

        private void OnLobbyLeft()
        {
            // Hide player list when leaving lobby
            gameObject.SetActive(false);
            ClearPlayerEntries();
        }
    }
}
