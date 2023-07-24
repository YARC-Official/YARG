using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using YARG.Core;
using YARG.Menu.Navigation;

namespace YARG.Menu
{
    [DefaultExecutionOrder(-25)]
    public class HelpBar : MonoBehaviour
    {
        public static HelpBar Instance { get; private set; }

        [SerializeField]
        private Transform _buttonContainer;

        [SerializeField]
        private TextMeshProUGUI _infoText;

        [Space]
        [SerializeField]
        private GameObject _buttonPrefab;

        [field: SerializeField]
        public MusicPlayer MusicPlayer { get; private set; }

        [Space]
        [SerializeField]
        private Color[] _menuActionColors;

        private readonly List<HelpBarButton> _buttons = new();

        private void Awake()
        {
            Instance = this;
            // Cache help bar buttons
            foreach (var _ in EnumExtensions<MenuAction>.Values)
            {
                var buttonObj = Instantiate(_buttonPrefab, _buttonContainer);
                var button = buttonObj.GetComponent<HelpBarButton>();
                _buttons.Add(button);
            }
        }

        private void OnDestroy()
        {
            Instance = null;
            foreach (Transform button in _buttonContainer)
            {
                Destroy(button.gameObject);
            }
            _buttons.Clear();
        }

        private void ResetHelpbar()
        {
            foreach (var button in _buttons)
            {
                button.gameObject.SetActive(false);
            }

            _infoText.text = null;
        }

        public void SetInfoFromScheme(NavigationScheme scheme)
        {
            ResetHelpbar();

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

                // Don't make buttons for up and down
                if (entry.Type == MenuAction.Up || entry.Type == MenuAction.Down)
                    continue;

                var button = _buttons[buttonIndex];
                button.gameObject.SetActive(true);
                button.SetInfoFromSchemeEntry(entry, _menuActionColors[(int) entry.Type]);
            }

            SetInfoText(string.Empty);
        }

        public void SetInfoText(string str)
        {
            if (str == String.Empty && !MusicPlayer.gameObject.activeInHierarchy)
            {
                _infoText.text = Constants.VERSION_TAG.ToString();
            }
            else
            {
                _infoText.text = str;
            }
        }
    }
}