using UnityEngine;
using UnityEngine.UI;
using YARG.Menu.Data;

namespace YARG.Menu
{
    public class IconButton : MonoBehaviour
    {
        [SerializeField]
        private Button _button;

        [Space]
        [SerializeField]
        private Image _background;
        [SerializeField]
        private Image _icon;

        public Image Icon
        {
            get => _icon;
            set
            {
                // Preserve the old color
                var color = _icon.color;

                // Create a copy of the icon so that
                _icon = Instantiate(_icon);
                _icon.color = color;
            }
        }

        public Color BackgroundColor
        {
            get => _background.color;
            set => _background.color = value;
        }

        public Button.ButtonClickedEvent OnClick => _button.onClick;

        /// <summary>
        /// Sets the background color, and updates the icon color based on the
        /// brightness of the background using the default light/dark icon colors.
        /// </summary>
        public void SetBackgroundAndIconColor(Color background)
        {
            _background.color = background;
            _icon.color = MenuData.Colors.GetBestTextColor(background);
        }

        /// <summary>
        /// Sets the background color, and updates the icon color based on the
        /// brightness of the background using the given light/dark icon colors.
        /// </summary>
        /// <param name="brightColor">
        /// The color to use to make the icon bright on dark backgrounds.
        /// </param>
        /// <param name="darkColor">
        /// The color to use to make the icon dark on bright backgrounds.
        /// </param>
        public void SetBackgroundAndIconColor(Color background, Color brightColor, Color darkColor)
        {
            _background.color = background;
            _icon.color = MenuColors.GetBestTextColor(background, brightColor, darkColor);
        }

        public void SetIconWithoutCopyOrColor(Image icon)
        {
            _icon = icon;
        }
    }
}