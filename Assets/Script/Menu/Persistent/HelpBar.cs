using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Core.Input;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    [DefaultExecutionOrder(-25)]
    public class HelpBar : MonoSingleton<HelpBar>
    {
        [SerializeField]
        private Transform _buttonContainer;

        [SerializeField]
        private TextMeshProUGUI _infoText;

        [Space]
        [SerializeField]
        private GameObject _buttonPrefab;

        [field: SerializeField]
        public MusicPlayer MusicPlayer { get; private set; }

        private readonly List<HelpBarButton> _buttons = new();

        protected override void SingletonAwake()
        {
            // Cache help bar buttons
            foreach (var _ in EnumExtensions<MenuAction>.Values)
            {
                var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
                var button = buttonObj.GetComponent<HelpBarButton>();
                _buttons.Add(button);
            }
        }

        protected override void SingletonDestroy()
        {
            _buttonContainer.DestroyChildren();
            _buttons.Clear();
        }

        private void ResetHelpBar()
        {
            foreach (var button in _buttons)
            {
                button.gameObject.SetActive(false);
            }

            _infoText.text = null;
        }

        public void SetInfoFromScheme(NavigationScheme scheme)
        {
            ResetHelpBar();

            // Show/hide music player
            if (GlobalVariables.Instance.CurrentScene == SceneIndex.Menu)
            {
                MusicPlayer.gameObject.SetActive(scheme.AllowsMusicPlayer);
            }
            else
            {
                MusicPlayer.gameObject.SetActive(false);
            }

            // Update buttons
            int buttonIndex = 0;
            foreach (var entry in scheme.Entries)
            {
                if (buttonIndex >= _buttons.Count)
                {
                    Debug.LogWarning("Too many actions in navigation scheme! Some actions will be ignored and unavailable.");
                    break;
                }

                // Skip actions without icons
                if (!GlobalVariables.Instance.MenuIcons.HasIcon(entry.Type))
                {
                    continue;
                }

                var button = _buttons[buttonIndex];
                button.gameObject.SetActive(true);
                button.SetInfoFromSchemeEntry(entry);

                buttonIndex++;
            }

            SetInfoText(string.Empty);
        }

        public void SetInfoText(string str)
        {
            if (str == string.Empty && !MusicPlayer.gameObject.activeInHierarchy)
            {
                _infoText.text = GlobalVariables.CurrentVersion.ToString();
            }
            else
            {
                _infoText.text = str;
            }
        }
    }
}