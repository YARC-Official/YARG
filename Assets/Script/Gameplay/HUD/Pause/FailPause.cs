using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Menu.Data;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Networking;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class FailPause : GenericPause
    {
        [SerializeField]
        private GameObject _separatorObject;

        private Button _restartButton;
        private Button _enableNoFailButton;
        private Button _backToLibraryButton;
        private Button _practiceModeButton;

        private TMP_Text _backToLibraryLabel;
        private TMP_Text _enableNoFailLabel;
        private TMP_Text _practiceModeLabel;

        private bool _buttonsCached;
        private bool _initialSeparatorState = true;
        private bool _initialRestartState = true;
        private bool _initialEnableNoFailState = true;
        private bool _initialPracticeModeState = true;

        private string _defaultBackToLibraryLabel;
        private string _defaultEnableNoFailLabel;
        private string _defaultPracticeModeLabel;

        private bool _isMultiplayer;
        private bool _isHost;

        protected override void GameplayAwake()
        {
            base.GameplayAwake();
            CacheButtons();
        }

        protected override void OnEnable()
        {
            CacheButtons();
            UpdateMultiplayerState();
            RestoreSinglePlayerLayout();

            if (_isMultiplayer)
            {
                ApplyMultiplayerLayout();
            }

            HandleNavigationScheme();
        }

        private async void HandleNavigationScheme()
        {
            await UniTask.WaitForSeconds(0.5f, true);

            if (!isActiveAndEnabled || Navigator.Instance == null)
            {
                return;
            }

                var entries = new List<NavigationScheme.Entry>
                {
                    NavigationScheme.Entry.NavigateSelect,
                    NavigationScheme.Entry.NavigateUp,
                    NavigationScheme.Entry.NavigateDown,
                };

                if (!_isMultiplayer)
                {
                    entries.Insert(1, new NavigationScheme.Entry(MenuAction.Red, "Menu.Common.Back", Back));
                }

                if (_isMultiplayer)
                {
                    entries.Add(_isHost
                        ? new NavigationScheme.Entry(MenuAction.Orange, "Back to Library", HostBackToLibrary)
                        : new NavigationScheme.Entry(MenuAction.Orange, "Leave Lobby", ClientLeaveLobby));
                }

            Navigator.Instance.PushScheme(new NavigationScheme(entries, false));
        }

        public override void Restart()
        {
            UpdateMultiplayerState();

            if (_isMultiplayer && _isHost)
            {
                HostRestart();
                return;
            }

            base.Restart();
        }

        public override void BackToLibrary()
        {
            UpdateMultiplayerState();

            if (!_isMultiplayer)
            {
                base.BackToLibrary();
                return;
            }

            if (_isHost)
            {
                HostBackToLibrary();
            }
            else
            {
                ClientLeaveLobby();
            }
        }

        // TODO: Make a similar option that only makes the rest of this song no fail
        //  and then resumes the song
        public void EnableNoFail()
        {
            // It feels a bit icky reaching down into the settings like this
            SettingsManager.Settings.NoFailMode.SetValueWithoutNotify(true);
            Restart();
        }

        private void CacheButtons()
        {
            if (_buttonsCached)
            {
                return;
            }

            _buttonsCached = true;

            if (_separatorObject != null)
            {
                _initialSeparatorState = _separatorObject.activeSelf;
            }

            foreach (var button in GetComponentsInChildren<Button>(true))
            {
                switch (button.gameObject.name)
                {
                    case "Restart":
                        _restartButton = button;
                        _initialRestartState = button.gameObject.activeSelf;
                        break;
                    case "Enable No Fail":
                        _enableNoFailButton = button;
                        _initialEnableNoFailState = button.gameObject.activeSelf;
                        _enableNoFailLabel = button.GetComponentInChildren<TMP_Text>(true);
                        if (_enableNoFailLabel != null)
                        {
                            _defaultEnableNoFailLabel = _enableNoFailLabel.text;
                        }
                        break;
                    case "Back to Library":
                        _backToLibraryButton = button;
                        _backToLibraryLabel = button.GetComponentInChildren<TMP_Text>(true);
                        if (_backToLibraryLabel != null)
                        {
                            _defaultBackToLibraryLabel = _backToLibraryLabel.text;
                        }
                        break;
                    case "Practice Mode":
                        _practiceModeButton = button;
                        _initialPracticeModeState = button.gameObject.activeSelf;
                        _practiceModeLabel = button.GetComponentInChildren<TMP_Text>(true);
                        if (_practiceModeLabel != null)
                        {
                            _defaultPracticeModeLabel = _practiceModeLabel.text;
                        }
                        break;
                }
            }

            if (_backToLibraryButton != null && string.IsNullOrEmpty(_defaultBackToLibraryLabel))
            {
                _backToLibraryLabel = _backToLibraryButton.GetComponentInChildren<TMP_Text>(true);
                if (_backToLibraryLabel != null)
                {
                    _defaultBackToLibraryLabel = _backToLibraryLabel.text;
                }
            }
        }

        private void UpdateMultiplayerState()
        {
            var manager = YargNetworkManager.Instance;
            _isMultiplayer = manager != null && manager.isNetworkActive;
            _isHost = _isMultiplayer && manager.IsHosting;
        }

        private void RestoreSinglePlayerLayout()
        {
            if (_separatorObject != null)
            {
                _separatorObject.SetActive(_initialSeparatorState);
            }

            if (_restartButton != null)
            {
                _restartButton.gameObject.SetActive(_initialRestartState);
            }

            if (_enableNoFailButton != null)
            {
                _enableNoFailButton.gameObject.SetActive(_initialEnableNoFailState);
            }

            if (_practiceModeButton != null)
            {
                _practiceModeButton.gameObject.SetActive(_initialPracticeModeState);
            }

            if (_backToLibraryButton != null)
            {
                _backToLibraryButton.gameObject.SetActive(true);
            }

            if (_backToLibraryLabel != null && !string.IsNullOrEmpty(_defaultBackToLibraryLabel))
            {
                _backToLibraryLabel.text = _defaultBackToLibraryLabel;
            }

            if (_enableNoFailLabel != null && !string.IsNullOrEmpty(_defaultEnableNoFailLabel))
            {
                _enableNoFailLabel.text = _defaultEnableNoFailLabel;
            }

            if (_practiceModeLabel != null && !string.IsNullOrEmpty(_defaultPracticeModeLabel))
            {
                _practiceModeLabel.text = _defaultPracticeModeLabel;
            }
        }

        private void ApplyMultiplayerLayout()
        {
            if (_separatorObject != null)
            {
                _separatorObject.SetActive(false);
            }

            if (_enableNoFailButton != null)
            {
                _enableNoFailButton.gameObject.SetActive(false);
            }

            if (_practiceModeButton != null)
            {
                _practiceModeButton.gameObject.SetActive(false);
            }

            if (_isHost)
            {
                if (_restartButton != null)
                {
                    _restartButton.gameObject.SetActive(true);
                }

                if (_backToLibraryLabel != null && !string.IsNullOrEmpty(_defaultBackToLibraryLabel))
                {
                    _backToLibraryLabel.text = _defaultBackToLibraryLabel;
                }
            }
            else
            {
                if (_restartButton != null)
                {
                    _restartButton.gameObject.SetActive(false);
                }

                if (_backToLibraryButton != null)
                {
                    _backToLibraryButton.gameObject.SetActive(true);
                }

                if (_backToLibraryLabel != null)
                {
                    _backToLibraryLabel.text = "Leave Lobby";
                }
            }
        }

        private void HostRestart()
        {
            if (!_isHost)
            {
                return;
            }

            var manager = YargNetworkManager.Instance;
            if (manager != null)
            {
                manager.RestartMultiplayerGameplay();
            }

            PauseMenuManager.Restart();
        }

        private void HostBackToLibrary()
        {
            if (!_isHost)
            {
                return;
            }

            YargNetworkManager.SetMenuNavigationAfterSceneLoad(
                Menu.MenuManager.Menu.OnlineMultiplayer,
                Menu.MenuManager.Menu.LobbyRoom,
                Menu.MenuManager.Menu.MusicLibrary);

            var manager = YargNetworkManager.Instance;
            if (manager != null)
            {
                manager.QuitMultiplayerGameplay();
            }

            PauseMenuManager.Quit();
        }

        private void ClientLeaveLobby()
        {
            if (!_isMultiplayer || _isHost)
            {
                return;
            }

            var dialogManager = DialogManager.Instance;
            if (dialogManager == null)
            {
                ExecuteClientLeaveLobby();
                return;
            }

            var dialog = dialogManager.ShowMessage(
                "Leave Lobby?",
                "Are you sure you want to leave the lobby? All players will be returned to the music library.");

            dialog.ClearButtons();
            dialog.AddDialogButton("Cancel", MenuData.Colors.BrightButton, () => dialogManager.ClearDialog());
            dialog.AddDialogButton("Leave Lobby", MenuData.Colors.CancelButton, () =>
            {
                dialogManager.ClearDialog();
                ExecuteClientLeaveLobby();
            });
        }

        private void ExecuteClientLeaveLobby()
        {
            var manager = YargNetworkManager.Instance;
            if (manager != null)
            {
                manager.LeaveLobby();
            }
        }
    }
}