using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Networking;
using YARG.Menu.Persistent;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Dialog for creating a new lobby.
    /// Uses YARG's dialog system for consistent UI.
    /// </summary>
    public class CreateLobbyDialog : MonoBehaviour
    {
        [Header("UI Elements")]
        [SerializeField] private TMP_InputField lobbyNameInput;
        [SerializeField] private TMP_Dropdown maxPlayersDropdown;
        [SerializeField] private TMP_Dropdown privacyModeDropdown;
        [SerializeField] private TMP_InputField passwordInput;
        [SerializeField] private GameObject passwordPanel;
        [SerializeField] private GameObject passwordLabelPanel; // Optional: separate panel for password label
        [SerializeField] private Button createButton;
        [SerializeField] private Button cancelButton;

        private void Start()
        {
            if (createButton != null)
            {
                createButton.onClick.AddListener(OnCreateClicked);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.AddListener(OnCancelClicked);
            }

            // Set up privacy mode dropdown
            if (privacyModeDropdown != null)
            {
                privacyModeDropdown.ClearOptions();
                privacyModeDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "Public",
                    "Private (Password)",
                    "Friends Only"
                });
                privacyModeDropdown.onValueChanged.AddListener(OnPrivacyModeChanged);
            }

            // Set up max players dropdown
            if (maxPlayersDropdown != null)
            {
                maxPlayersDropdown.ClearOptions();
                var playerOptions = new System.Collections.Generic.List<string>();
                for (int i = 2; i <= 32; i++)
                {
                    playerOptions.Add(i.ToString());
                }
                maxPlayersDropdown.AddOptions(playerOptions);
                maxPlayersDropdown.value = 6; // Default to 8 players
            }

            // Set default lobby name
            if (lobbyNameInput != null)
            {
                lobbyNameInput.text = $"{YargNetworkManager.Instance.PlayerName}'s Lobby";
            }

            OnPrivacyModeChanged(0);
        }

        private void OnPrivacyModeChanged(int index)
        {
            // Show password field for private lobbies
            bool showPassword = index == 1;
            
            if (passwordPanel != null)
            {
                passwordPanel.SetActive(showPassword);
            }
            
            // Also show/hide password label panel if it exists
            if (passwordLabelPanel != null)
            {
                passwordLabelPanel.SetActive(showPassword);
            }
        }

        public void OnCreateClicked()
        {
            string lobbyName = lobbyNameInput != null ? lobbyNameInput.text : "YARG Lobby";
            if (string.IsNullOrEmpty(lobbyName))
            {
                lobbyName = "YARG Lobby";
            }

            int maxPlayers = maxPlayersDropdown != null ? maxPlayersDropdown.value + 2 : 8;
            var privacyMode = privacyModeDropdown != null 
                ? (YargNetworkManager.LobbyPrivacyMode)privacyModeDropdown.value 
                : YargNetworkManager.LobbyPrivacyMode.Public;
            string password = (privacyMode == YargNetworkManager.LobbyPrivacyMode.Private && passwordInput != null) 
                ? passwordInput.text 
                : "";

            // Close this dialog first
            gameObject.SetActive(false);
            
            YargNetworkManager.Instance.CreateLobby(lobbyName, maxPlayers, privacyMode, password);
            
            // Don't show success message here - OnLobbyJoined will handle it
            // This prevents dialog spam when another dialog is already showing
        }

        public void OnCancelClicked()
        {
            // Close this dialog
            gameObject.SetActive(false);
        }
    }
}