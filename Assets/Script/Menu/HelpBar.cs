using System;
using TMPro;
using UnityEngine;
using YARG.Input;

namespace YARG.UI
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

        private void Awake()
        {
            Instance = this;
        }

        private void ResetHelpbar()
        {
            foreach (Transform button in _buttonContainer)
            {
                Destroy(button.gameObject);
            }

            _infoText.text = null;
        }

        public void SetInfoFromScheme(NavigationScheme scheme)
        {
            ResetHelpbar();

            // Show/hide music player
            if (GameManager.Instance.CurrentScene == SceneIndex.MENU)
            {
                MusicPlayer.gameObject.SetActive(scheme.AllowsMusicPlayer);
            }
            else
            {
                MusicPlayer.gameObject.SetActive(false);
            }

            // Spawn all buttons
            foreach (var entry in scheme.Entries)
            {
                // Don't make buttons for up and down
                if (entry.Type == MenuAction.Up || entry.Type == MenuAction.Down)
                {
                    continue;
                }

                var go = Instantiate(_buttonPrefab, _buttonContainer);
                go.GetComponent<HelpBarButton>().SetInfoFromSchemeEntry(entry, _menuActionColors[(int) entry.Type]);
            }

            SetInfoText(String.Empty);
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