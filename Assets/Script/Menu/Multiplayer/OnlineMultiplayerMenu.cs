using UnityEngine;
using UnityEngine.UI;
using TMPro;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Networking;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Localization;

namespace YARG.Menu.Multiplayer
{
    /// <summary>
    /// Main menu for online multiplayer.
    /// Handles navigation between lobby browser, create lobby, and direct connect.
    /// </summary>
    public class OnlineMultiplayerMenu : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI headerText;
        [SerializeField] private TextMeshProUGUI connectionStatusText;
        
        [Header("Dialogs")]
        [SerializeField] private GameObject createLobbyDialog;
        [SerializeField] private GameObject directConnectDialog;

        private void Start()
        {
            // Subscribe to network events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyCreated += OnLobbyCreated;
                YargNetworkManager.Instance.OnLobbyJoined += OnLobbyJoined;
                YargNetworkManager.Instance.OnLobbyLeft += OnLobbyLeft;
                YargNetworkManager.Instance.OnNetworkError += OnNetworkError;
            }

            UpdateConnectionStatus();
        }

        private void OnEnable()
        {
            // Reset join flag when menu is reopened
            _hasJoinedLobby = false;
            
            // Set up navigation scheme (following YARG's pattern)
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", OnBackClicked),
            }, true));
        }

        private void OnDisable()
        {
            Navigator.Instance?.PopScheme();
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.OnLobbyCreated -= OnLobbyCreated;
                YargNetworkManager.Instance.OnLobbyJoined -= OnLobbyJoined;
                YargNetworkManager.Instance.OnLobbyLeft -= OnLobbyLeft;
                YargNetworkManager.Instance.OnNetworkError -= OnNetworkError;
            }
        }

        // Button callbacks (to be connected in Unity Inspector via UI button prefabs)
        public void OnFindLobbyClicked()
        {
            // Open the lobby browser menu
            YargLogger.LogInfo("[OnlineMultiplayerMenu] Find Lobby clicked");
            MenuManager.Instance.PushMenu(MenuManager.Menu.LobbyBrowser);
        }

        public void OnCreateLobbyClicked()
        {
            if (createLobbyDialog != null)
            {
                YargLogger.LogInfo("[OnlineMultiplayerMenu] Create Lobby clicked");
                createLobbyDialog.SetActive(true);
            }
            else
            {
                YargLogger.LogWarning("[OnlineMultiplayerMenu] Create Lobby Dialog not assigned in Inspector!");
            }
        }

        public void OnDirectConnectClicked()
        {
            if (directConnectDialog != null)
            {
                YargLogger.LogInfo("[OnlineMultiplayerMenu] Direct Connect clicked");
                directConnectDialog.SetActive(true);
            }
            else
            {
                YargLogger.LogWarning("[OnlineMultiplayerMenu] Direct Connect Dialog not assigned in Inspector!");
            }
        }

        public void OnBackClicked()
        {
            MenuManager.Instance.PopMenu();
        }

        private void OnLobbyCreated(YargNetworkManager.LobbyInfo lobby)
        {
            YargLogger.LogInfo($"[OnlineMultiplayerMenu] Lobby created: {lobby.lobbyName}");
            UpdateConnectionStatus();
            
            // Don't show dialog or navigate here - the host's own OnLobbyJoined will handle it
            // This prevents double-navigation and dialog spam
        }

        private bool _hasJoinedLobby = false;

        private void OnLobbyJoined(YargNetworkManager.LobbyInfo lobby)
        {
            // Prevent multiple calls (Mirror can trigger this multiple times)
            if (_hasJoinedLobby)
            {
                YargLogger.LogInfo("[OnlineMultiplayerMenu] Already joined lobby, ignoring duplicate call");
                return;
            }
            
            _hasJoinedLobby = true;
            YargLogger.LogInfo($"[OnlineMultiplayerMenu] Joined lobby: {lobby.lobbyName}");
            UpdateConnectionStatus();
            
            // Dismiss any connecting dialogs
            if (DialogManager.Instance != null && DialogManager.Instance.IsDialogShowing)
            {
                DialogManager.Instance.ClearDialog();
            }
            
            // Navigate to lobby room (waiting room before song selection)
            // The LobbyRoomMenu will show all the lobby info, no need for a dialog here
            MenuManager.Instance.PushMenu(MenuManager.Menu.LobbyRoom);
        }

        private void OnLobbyLeft()
        {
            YargLogger.LogInfo("[OnlineMultiplayerMenu] Left lobby");
            _hasJoinedLobby = false;
            UpdateConnectionStatus();
        }

        private void OnNetworkError(string error)
        {
            YargLogger.LogError($"[OnlineMultiplayerMenu] Network error: {error}");
            
            // Show error dialog using YARG's DialogManager
            if (DialogManager.Instance != null)
            {
                if (DialogManager.Instance.IsDialogShowing)
                {
                    DialogManager.Instance.ClearDialog();
                }

                DialogManager.Instance.ShowMessage(Localize.Key("Menu", "LobbyBrowser", "ConnectionErrorTitle"), error);
            }

            if (connectionStatusText != null)
            {
                connectionStatusText.text = Localize.KeyFormat(("Menu", "LobbyBrowser", "StatusError"), error);
                connectionStatusText.color = Color.red;
            }
        }

        private void UpdateConnectionStatus()
        {
            if (connectionStatusText == null) return;

            var networkManager = YargNetworkManager.Instance;

            if (networkManager != null && networkManager.LocalUserIsHost())
            {
                connectionStatusText.text = Localize.Key("Menu", "LobbyBrowser", "StatusHosting");
                connectionStatusText.color = Color.green;
            }
            else if (networkManager != null && networkManager.CurrentLobby != null)
            {
                // Check if we're connected by seeing if we have a current lobby
                connectionStatusText.text = Localize.Key("Menu", "LobbyBrowser", "StatusConnected");
                connectionStatusText.color = Color.green;
            }
            else
            {
                connectionStatusText.text = Localize.Key("Menu", "LobbyBrowser", "StatusNotConnected");
                connectionStatusText.color = Color.white;
            }
        }
    }
}