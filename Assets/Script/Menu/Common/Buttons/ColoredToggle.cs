using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;

namespace YARG.Menu
{
    public class ColoredToggle : MonoBehaviour
    {
        [SerializeField]
        private Toggle _toggle;

        [SerializeField]
        private Image _background;

        [SerializeField]
        private TextMeshProUGUI _text;

        public Toggle.ToggleEvent OnToggled => _toggle.onValueChanged;

        public void SetBackgroundAndTextColor(bool isOn)
        {
            _background.color = !isOn ? MenuData.Colors.DeactivatedButton : MenuData.Colors.BrightButton;
            _text.color = !isOn ? MenuData.Colors.DeactivatedText : MenuData.Colors.DarkText;
        }
    }
}
