using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;

namespace YARG
{
    public class ActivePlayerListItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _PlayerNameText;
        [SerializeField]
        private Image _playerGameModeIcon;

        [SerializeField]
        private YargProfile _profile;
        public YargProfile Profile
        {
            get
            {
                return _profile;
            }
            set
            {
                _profile = value;

                Debug.Assert(_profile != null);
                _PlayerNameText.text = _profile.Name;
                _playerGameModeIcon.sprite = _gameModeIcons[(int) _profile.GameMode];
            }
        }

        public bool ShowName
        {
            get => _PlayerNameText.gameObject.activeSelf;
            set => _PlayerNameText.gameObject.SetActive(value);
        }
        [SerializeField]
        private Sprite[] _gameModeIcons = new Sprite[0];
    }
}
