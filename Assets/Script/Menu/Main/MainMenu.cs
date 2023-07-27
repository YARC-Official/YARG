using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
                new NavigationScheme.Entry(MenuAction.Confirm, "Quickplay", () =>
                {
                    MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.MusicLibrary);
                })
            }, true));
        }
        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public void QuickPlay()
        {
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.MusicLibrary);
        }

        public void Practice()
        {
            // MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.MusicLibrary);
        }

        public void Profiles()
        {
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.Profiles);
        }

        public void Replays()
        {
            MenuNavigator.Instance.PushMenu(MenuNavigator.Menu.Replays);
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