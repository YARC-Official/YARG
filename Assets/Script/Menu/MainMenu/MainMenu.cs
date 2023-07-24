using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;
using YARG.Menu.Settings;
using YARG.Player.Input;
using YARG.Menu.Navigation;
using YARG.Settings;
using YARG.Song;

namespace YARG.Menu
{
    public class MainMenu : MonoBehaviour
    {
        private enum ButtonIndex
        {
            QUICKPLAY,
            ADD_EDIT_PLAYERS,
            SETTINGS,
            CREDITS,
            EXIT
        }

        [FormerlySerializedAs("versionText")]
        [SerializeField]
        private TextMeshProUGUI _versionText;

        [FormerlySerializedAs("menuButtons")]
        [Space]
        [SerializeField]
        private Button[] _menuButtons;

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

            // Change quickplay text if no player
            var quickplayText = _menuButtons[(int) ButtonIndex.QUICKPLAY].GetComponentInChildren<TextMeshProUGUI>();
            if (PlayerManager.players.Count > 0)
            {
                quickplayText.text = "QUICKPLAY";
            }
            else
            {
                quickplayText.text = "<color=#808080>QUICKPLAY<size=50%> (Add a player!)</size></color>";
            }
        }

        private void OnDisable()
        {
            Navigator.Instance.PopScheme();
        }

        public void ShowSettingsMenu()
        {
            SettingsMenu.Instance.gameObject.SetActive(true);
        }

        public void Quit()
        {
#if UNITY_EDITOR

            UnityEditor.EditorApplication.isPlaying = false;

#else
			Application.Quit();

#endif
        }
    }
}