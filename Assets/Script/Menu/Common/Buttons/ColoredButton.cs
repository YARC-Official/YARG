using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using YARG.Menu.Data;

namespace YARG.Menu
{
    public class ColoredButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField]
        private Button _button;

        [Space]
        [SerializeField]
        private Image[] _backgroundImages;
        [SerializeField]
        private TextMeshProUGUI _text;

        public TextMeshProUGUI Text => _text;

        private Color _originalBackgroundColor;
        private Color _backgroundColor;
        public Color BackgroundColor
        {
            get => _backgroundColor;
            private set
            {
                _backgroundColor = value;

                foreach (var image in _backgroundImages)
                {
                    image.color = value;
                }
            }
        }

        private void OnEnable()
        {
            // Save the original color of the first image
            if (_backgroundImages.Length > 0)
            {
                _originalBackgroundColor = _backgroundImages[0].color;
            }
        }

        public Button.ButtonClickedEvent OnClick => _button.onClick;
        public Action<ColoredButton> PointerEnter, PointerExit;

        /// <summary>
        /// Sets the background color, and updates the text color based on the
        /// brightness of the background using the default light/dark text colors.
        /// </summary>
        public void SetBackgroundAndTextColor(Color background)
        {
            BackgroundColor = background;
            _text.color = MenuData.Colors.GetBestTextColor(background);
        }

        /// <summary>
        /// Disables the button and sets the background color to gray to denote this
        /// </summary>
        public void DisableButton()
        {
            _button.interactable = false;
            SetBackgroundAndTextColor(Color.gray);
        }

        /// <summary>
        /// Enables the button and sets the background color to the original color
        /// </summary>
        public void EnableButton()
        {
            _button.interactable = true;
            SetBackgroundAndTextColor(_originalBackgroundColor);
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
            BackgroundColor = background;
            _text.color = MenuColors.GetBestTextColor(background, brightColor, darkColor);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            PointerEnter?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            PointerExit?.Invoke(this);
        }
    }
}