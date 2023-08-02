using TMPro;
using UnityEngine;
using YARG.Menu.MusicLibrary;
using YARG.Menu.Settings;
using YARG.Menu.Navigation;

namespace YARG.Menu.Main
{
    public class MainMenu : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _versionText;

        private void Start()
        {
            _versionText.text = GlobalVariables.CurrentVersion.ToString();
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

            var musicLibraryMenu = menu.GetComponent<MusicLibraryMenu>();
            musicLibraryMenu.LibraryMode = MusicLibraryMode.QuickPlay;

            menu.gameObject.SetActive(true);
        }

        public void Practice()
        {
            var menu = MenuManager.Instance.PushMenu(MenuManager.Menu.MusicLibrary, false);

            var musicLibraryMenu = menu.GetComponent<MusicLibraryMenu>();
            musicLibraryMenu.LibraryMode = MusicLibraryMode.Practice;

            menu.gameObject.SetActive(true);
        }

        public void Profiles()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.Profiles);
        }

        public void Replays()
        {
            MenuManager.Instance.PushMenu(MenuManager.Menu.Replays);
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