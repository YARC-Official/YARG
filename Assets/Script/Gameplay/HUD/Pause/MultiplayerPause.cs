using UnityEngine;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Networking;

namespace YARG.Gameplay.HUD
{
    /// <summary>
    /// Pause menu for online multiplayer gameplay.
    /// Host can restart, toggle practice, and return all players to library.
    /// Clients can leave lobby (disconnecting all players).
    /// </summary>
    public class MultiplayerPause : GenericPause
    {
        [Header("UI Elements (Optional - for button visibility)")]
        [SerializeField]
        private GameObject _restartButton;
        [SerializeField]
        private GameObject _togglePracticeButton;
        [SerializeField]
        private GameObject _backToLibraryButton;
        [SerializeField]
        private GameObject _leaveLobbyButton;
        
        private bool _isHost;

        protected override void OnEnable()
        {
            // Don't call base.OnEnable() - we'll set up our own navigation scheme
            
            _isHost = YargNetworkManager.Instance != null && YargNetworkManager.Instance.LocalUserIsHost();
            
            // Show/hide buttons based on role
            UpdateButtonVisibility();
            
            // Create navigation scheme based on role
            var entries = new System.Collections.Generic.List<NavigationScheme.Entry>
            {
                NavigationScheme.Entry.NavigateSelect,
                new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back),
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            };
            
            // Add role-specific actions
            if (_isHost)
            {
                entries.Add(new NavigationScheme.Entry(MenuAction.Orange, "Back to Library", HostBackToLibrary));
            }
            else
            {
                entries.Add(new NavigationScheme.Entry(MenuAction.Orange, "Leave Lobby", ClientLeaveLobby));
            }
            
            Navigator.Instance.PushScheme(new NavigationScheme(entries, false));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }
        
        private void UpdateButtonVisibility()
        {
            // Host buttons
            if (_restartButton != null)
                _restartButton.SetActive(_isHost);
            if (_togglePracticeButton != null)
                _togglePracticeButton.SetActive(_isHost);
            if (_backToLibraryButton != null)
                _backToLibraryButton.SetActive(_isHost);
                
            // Client buttons
            if (_leaveLobbyButton != null)
                _leaveLobbyButton.SetActive(!_isHost);
        }

        /// <summary>
        /// Host action: Restarts the song for all players.
        /// Called from UI button.
        /// </summary>
        public override void Restart()
        {
            if (!_isHost)
            {
                Debug.LogWarning("[MultiplayerPause] Only host can restart in multiplayer");
                return;
            }
            
            Debug.Log("[MultiplayerPause] Host restarting song for all players");
            
            // Sync all clients to restart
            // The scene reload will happen for everyone via GlobalVariables.LoadScene
            if (YargNetworkManager.Instance != null)
            {
                // Send RPC to all clients to reload the gameplay scene
                YargNetworkManager.Instance.RestartMultiplayerGameplay();
            }
            
            // Restart for host
            PauseMenuManager.Restart();
        }
        
        /// <summary>
        /// Host action: Toggles practice mode and restarts for all players.
        /// Called from UI button.
        /// </summary>
        public void HostTogglePractice()
        {
            if (!_isHost)
            {
                Debug.LogWarning("[MultiplayerPause] Only host can toggle practice in multiplayer");
                return;
            }
            
            Debug.Log("[MultiplayerPause] Host toggling practice mode for all players");
            
            // Toggle practice state
            GlobalVariables.State.IsPractice = !GlobalVariables.State.IsPractice;
            
            // Sync practice state to all clients
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.SyncPracticeMode(GlobalVariables.State.IsPractice);
                
                // Restart gameplay for all players
                YargNetworkManager.Instance.RestartMultiplayerGameplay();
            }
            
            // Restart for host
            PauseMenuManager.Restart();
        }
        
        /// <summary>
        /// Host action: Brings all players back to music library.
        /// Called from UI button or navigation.
        /// </summary>
        public void HostBackToLibrary()
        {
            if (!_isHost)
            {
                Debug.LogWarning("[MultiplayerPause] Only host can return to library in multiplayer");
                return;
            }
            
            Debug.Log("[MultiplayerPause] Host returning all players to music library");
            
            // Set the navigation target for everyone (host and clients)
            // MenuManager will navigate to MusicLibrary after Menu scene loads
            YargNetworkManager.SetMenuNavigationAfterSceneLoad(
                Menu.MenuManager.Menu.OnlineMultiplayer,
                Menu.MenuManager.Menu.LobbyRoom,
                Menu.MenuManager.Menu.MusicLibrary);
            
            // Tell all clients to quit and return to Menu scene
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.QuitMultiplayerGameplay();
            }
            
            // Quit song for host - this will load Menu scene
            PauseMenuManager.Quit();
        }
        
        /// <summary>
        /// Client action: Disconnects from lobby, bringing all players back to music library.
        /// Called from UI button.
        /// </summary>
        public void ClientLeaveLobby()
        {
            Debug.Log("[MultiplayerPause] ClientLeaveLobby called!");
            
            if (_isHost)
            {
                Debug.LogWarning("[MultiplayerPause] Host should use HostBackToLibrary instead");
                return;
            }
            
            Debug.Log("[MultiplayerPause] Showing leave lobby dialog...");
            
            // Show confirmation dialog
            ShowLeaveLobbyDialog();
        }
        
        private void ShowLeaveLobbyDialog()
        {
            if (DialogManager.Instance == null)
            {
                Debug.LogWarning("[MultiplayerPause] DialogManager.Instance is null");
                // If no dialog manager, just leave directly
                ExecuteClientLeaveLobby();
                return;
            }
            
            var dialog = DialogManager.Instance.ShowMessage(
                "Leave Lobby?",
                "Are you sure you want to leave the lobby? All players will be returned to the music library.");
            
            dialog.ClearButtons();
            dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, () => DialogManager.Instance.ClearDialog());
            dialog.AddDialogButton("Leave Lobby", MenuData.Colors.CancelButton, () =>
            {
                DialogManager.Instance.ClearDialog();
                ExecuteClientLeaveLobby();
            });
        }
        
        private void ExecuteClientLeaveLobby()
        {
            Debug.Log("[MultiplayerPause] Client leaving lobby - will disconnect all players");
            
            // The disconnect will trigger OnClientDisconnectedDuringGameplay on the host,
            // which will bring all players back to music library.
            // For this client, OnLobbyLeftDuringGameplay will be triggered,
            // which will bring them back to lobby browser.
            if (YargNetworkManager.Instance != null)
            {
                YargNetworkManager.Instance.LeaveLobby();
            }
            else
            {
                Debug.LogError("[MultiplayerPause] YargNetworkManager.Instance is null!");
            }
        }
    }
}
