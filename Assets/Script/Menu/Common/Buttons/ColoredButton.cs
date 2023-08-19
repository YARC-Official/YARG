using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Menu
{
    public class ColoredButton : MonoBehaviour
    {
        public static readonly Color BrightTextColor = Color.white;
        public static readonly Color DarkTextColor = new Color32(0x00, 0x3A, 0x47, 0xFF);

        public static readonly Color BrightBackgroundColor = new Color32(0x2E, 0xD9, 0xFF, 0xFF);
        public static readonly Color DarkBackgroundColor = new Color32(0x27, 0x36, 0x47, 0xFF);

        public static readonly Color ConfirmColor = new Color32(0x17, 0xE2, 0x89, 0xFF);
        public static readonly Color CancelColor = new Color32(0xF3, 0x2B, 0x37, 0xFF);

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
            SetBackgroundAndTextColor(background, BrightTextColor, DarkTextColor);
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
            _text.color = GetBestTextColor(background, brightColor, darkColor);
        }

        /// <summary>
        /// Determines the best text color to use with the given background color.
        /// </summary>
        /// <param name="brightColor">
        /// The color to use to make the text bright on dark backgrounds.
        /// </param>
        /// <param name="darkColor">
        /// The color to use to make the text dark on bright backgrounds.
        /// </param>
        public static Color GetBestTextColor(Color background, Color brightColor, Color darkColor)
        {
            static float GetLightness(Color color)
            {
                // These values work a lot better for this scenario
                return color.GetLightness(0.45f, 0.40f, 0.15f);
            }

            // Threshold above which a color is considered to be bright,
            // and below which it is considered dark
            const float lightnessThreshold = 0.5f;

            // Use the lightness difference between the text colors as the threshold range
            float brightLightness = GetLightness(brightColor);
            float darkLightness = GetLightness(darkColor);
            float threshold = Mathf.Lerp(darkLightness, brightLightness, lightnessThreshold);

            // Determine if the background color's lightness
            float lightness = GetLightness(background);
            return lightness < threshold ? brightColor : darkColor;
        }
    }
}