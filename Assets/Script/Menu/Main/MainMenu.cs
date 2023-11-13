using TMPro;
using UnityEngine;
using YARG.Helpers;
using YARG.Menu.Data;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Settings;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Settings;

namespace YARG.Menu.Main
{
    public class MainMenu : MonoBehaviour
    {
        private static bool _antiPiracyDialogShown;

        [SerializeField]
        private TextMeshProUGUI _versionText;

        private void Start()
        {
            _versionText.text = GlobalVariables.CURRENT_VERSION.ToString();

            // Show the anti-piracy dialog if it hasn't been shown already
            // Also only show it once per game launch
            if (!_antiPiracyDialogShown && SettingsManager.Settings.ShowAntiPiracyDialog)
            {
                var dialog = DialogManager.Instance.ShowOneTimeMessage(
                    LocaleHelper.LocalizeString("Dialogs.AntiPiracy.Title"),
                    LocaleHelper.LocalizeString("Dialogs.AntiPiracy"),
                    () =>
                    {
                        SettingsManager.Settings.ShowAntiPiracyDialog = false;
                        SettingsManager.SaveSettings();
                    });

                dialog.ClearButtons();
                dialog.AddDialogButton("I Understand", MenuData.Colors.ConfirmButton,
                    DialogManager.Instance.ClearDialog);

                _antiPiracyDialogShown = true;
            }
        }

        private void OnEnable()
        {
            // Set navigation scheme
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                NavigationScheme.Entry.NavigateSelect,
                NavigationScheme.Entry.NavigateUp,
                NavigationScheme.Entry.NavigateDown,
            }, true));
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public void QuickPlay()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);

            MusicLibraryMenu.LibraryMode = MusicLibraryMode.QuickPlay;

            menu.gameObject.SetActive(true);
        }

        public void Practice()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);

            MusicLibraryMenu.LibraryMode = MusicLibraryMode.Practice;

            menu.gameObject.SetActive(true);
        }

        public void Profiles()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.Profiles);
        }

        public void Replays()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.History);
        }

        public void Settings()
        {
            SettingsMenu.Instance.gameObject.SetActive(true);
        }

        public void Exit()
        {
#if UNITY_EDITOR

            UnityEditor.EditorApplication.isPlaying = false;

#else
			Application.Quit();

#endif
        }

        public void OpenDiscord()
        {
            Application.OpenURL("https://discord.gg/sqpu4R552r");
        }

        public void OpenGithub()
        {
            Application.OpenURL("https://github.com/YARC-Official/YARG");
        }
    }
}