using UnityEngine;
using YARG.Helpers.Extensions;

namespace YARG.Menu.Data
{
    [CreateAssetMenu(fileName = "MenuColors", menuName = "YARG/Menu Colors")]
    public class MenuColors : ScriptableObject
    {
        [Header("Text")]
        public Color BrightText;
        public Color DarkText;
        public Color PrimaryText;
        public Color DeactivatedText;

        [Header("Button")]
        public Color BrightButton;
        public Color DarkButton;
        public Color ConfirmButton;
        public Color CancelButton;
        public Color DeactivatedButton;

        [Header("Navigation")]
        public Color NavigationGreen;
        public Color NavigationRed;
        public Color NavigationYellow;
        public Color NavigationBlue;
        public Color NavigationOrange;

        [Header("MusicLibrary")]
        public Color TrackDefaultPrimary;
        public Color TrackDefaultSecondary;
        public Color TrackSelectedPrimary;
        public Color TrackSelectedSecondary;
        public Color ActionPrimary;
        public Color ActionSecondary;
        public Color SetlistPrimary;
        public Color SetlistSecondary;
        public Color HeaderPrimary;
        public Color HeaderSecondary;
        public Color HeaderTertiary;
        public Color SubheaderPrimary;
        public Color HeaderSelectedPrimary;

        /// <summary>
        /// Determines the best text color to use with the given background color,
        /// using default bright/dark text colors.
        /// </summary>
        public Color GetBestTextColor(Color background)
        {
            return GetBestTextColor(background, BrightText, DarkText);
        }

        /// <summary>
        /// Determines the best text color to use with the given background color,
        /// using the given bright/dark text colors.
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
                return color.GetLightness(0.30f, 0.40f, 0.15f);
            }

            // Threshold above which a color is considered to be bright,
            // and below which it is considered dark
            const float LIGHTNESS_THRESHOLD = 0.58f;

            // Use the lightness difference between the text colors as the threshold range
            float brightLightness = GetLightness(brightColor);
            float darkLightness = GetLightness(darkColor);
            float threshold = Mathf.Lerp(darkLightness, brightLightness, LIGHTNESS_THRESHOLD);

            // Determine if the background color's lightness
            float lightness = GetLightness(background);
            return lightness < threshold ? brightColor : darkColor;
        }
    }
}
