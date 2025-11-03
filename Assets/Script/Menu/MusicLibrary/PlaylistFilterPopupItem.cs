using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Navigation;

namespace YARG.Menu.MusicLibrary
{
    public class PlaylistFilterPopupItem : MonoBehaviour
    {
        [SerializeField]
        private TextMeshProUGUI _body;
        [SerializeField]
        private Toggle _toggle;

        [field: SerializeField]
        public NavigatableButton Button { get; private set; }

        private System.Action<string, bool> _onToggleChanged;
        private string _playlistName;

        public void Initialize(string playlistName, bool isSelected, System.Action<string, bool> onToggleChanged)
        {
            _playlistName = playlistName;
            _body.text = playlistName;
            _toggle.isOn = isSelected;
            _onToggleChanged = onToggleChanged;

            // When the button is clicked, toggle the checkbox
            Button.SetOnClickEvent(() =>
            {
                _toggle.isOn = !_toggle.isOn;
                _onToggleChanged?.Invoke(_playlistName, _toggle.isOn);
            });

            // Also handle direct toggle interaction
            _toggle.onValueChanged.AddListener((isOn) =>
            {
                _onToggleChanged?.Invoke(_playlistName, isOn);
            });
        }

        public void SetSelected(bool isSelected)
        {
            _toggle.isOn = isSelected;
        }
    }
}
