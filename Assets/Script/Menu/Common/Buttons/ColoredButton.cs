using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;

namespace YARG.Menu
{
    public class ColoredButton : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [Space]
        [SerializeField]
        private Image _background;
        [SerializeField]
        private TextMeshProUGUI _text;

        public TextMeshProUGUI Text => _text;

        public Color BackgroundColor
        {
            get => _background.color;
            set => _background.color = value;
        }

        public Button.ButtonClickedEvent OnClick => _button.onClick;

        /// <summary>
        /// Sets the background color, and updates the text color based on the
        /// brightness of the background using the default light/dark text colors.
        /// </summary>
        public void SetBackgroundAndTextColor(Color background)
        {
            _background.color = background;
            _text.color = MenuData.Colors.GetBestTextColor(background);
        }

        /// <summary>
        /// Sets the background color, and updates the text color based on the
        /// brightness of the background using the given light/dark text colors.
        /// </summary>
        /// <param name="brightColor">
        /// The color to use to make the text bright on dark backgrounds.
        /// </param>
        /// <param name="darkColor">
        /// The color to use to make the text dark on bright backgrounds.
        /// </param>
        public void SetBackgroundAndTextColor(Color background, Color brightColor, Color darkColor)
        {
            _background.color = background;
            _text.color = MenuColors.GetBestTextColor(background, brightColor, darkColor);
        }
    }
}