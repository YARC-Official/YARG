using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core.Extensions;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;
using YARG.Menu.Data;
using YARG.Menu.Navigation;

namespace YARG.Menu.Persistent
{
    [DefaultExecutionOrder(-25)]
    public class HelpBar : MonoSingleton<HelpBar>
    {
        [SerializeField]
        private Transform _buttonContainer;

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
        }

        public void Reset()
        {
            ResetHelpBar();
            gameObject.SetActive(false);
        }

        public void SetInfoFromScheme(NavigationScheme scheme)
        {
            ResetHelpBar();

            // Show/hide music player
            if (GlobalVariables.Instance.CurrentScene == SceneIndex.Menu)
            {
                // Preserve music player state if value is not set
                if (scheme.AllowsMusicPlayer is {} allowed)
                    MusicPlayer.gameObject.SetActive(allowed);
            }
            else
            {
                MusicPlayer.gameObject.SetActive(false);
            }

            if (scheme.SuppressHelpBar)
            {
                // We want the black bar, but no buttons
                gameObject.SetActive(true);
                return;
            }

            // Update buttons
            int buttonIndex = 0;
            foreach (var entry in scheme.Entries)
            {
                if (buttonIndex >= _buttons.Count)
                {
                    YargLogger.LogWarning("Too many actions in navigation scheme! Some actions will be ignored and unavailable.");
                    break;
                }

                // Skip actions without icons
                if (!MenuData.NavigationIcons.HasIcon(entry.Action))
                {
                    continue;
                }

                var button = _buttons[buttonIndex];
                button.gameObject.SetActive(true);
                button.SetInfoFromSchemeEntry(entry);

                buttonIndex++;
            }

            gameObject.SetActive(true);
        }
    }
}