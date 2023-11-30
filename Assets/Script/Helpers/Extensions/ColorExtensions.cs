using System.Runtime.CompilerServices;
using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class ColorExtensions
    {
        public static Color ToUnityColor(this System.Drawing.Color color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

        public static System.Drawing.Color ToSystemColor(this Color color)
        {
            return System.Drawing.Color.FromArgb(
                (int) (color.a * 255),
                (int) (color.r * 255),
                (int) (color.g * 255),
                (int) (color.b * 255));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Color WithAlpha(this Color col, float alpha)
        {
            return new Color(col.r, col.g, col.b, alpha);
        }

        #region Brightness values - https://stackoverflow.com/a/56678483 https://poynton.ca/GammaFAQ.html
        /// <summary>
        /// Calculates the linear brightness of the color, using the given weights for each color channel.
        /// </summary>
        public static float GetLuminance(this Color color, float redFactor, float greenFactor, float blueFactor)
        {
            // This calculation must be done in linear space
            color = color.linear;
            return color.r * redFactor +
                color.g * greenFactor +
                color.b * blueFactor;
        }

        /// <summary>
        /// Calculates the linear brightness of the color, using the ITU BT.709 specification.<br/>
        /// Gives more weight to the green channel.
        /// </summary>
        public static float GetLuminance_BT709(this Color color) => color.GetLuminance(0.2126f, 0.7152f, 0.0722f);

        /// <summary>
        /// Calculates the linear brightness of the color, using the ITU BT.601 specification.<br/>
        /// Gives more weight to the red and blue channels.
        /// </summary>
        public static float GetLuminance_BT601(this Color color) => color.GetLuminance(0.299f, 0.587f, 0.114f);

        /// <summary>
        /// Converts the given linear luminance to non-linear, perceptual lightness.
        /// </summary>
        public static float GetLightness(float luminance)
        {
            // There is a linear segment to this calculation being skipped for brevity,
            // it covers such a small range (around 0-0.008) that it's unimportant
            // It can be skipped as long as the output's lower bound is limited to 0

            // These values have been modified a bit to output 0-1 instead of 0-100
            const float factor = 1.16f; // 116
            const float offset = -0.16f; // -16

            // Cube root power
            const float cubeRoot = 1f / 3f;

            float lightness = Mathf.Pow(luminance, cubeRoot) * factor + offset;
            return Mathf.Max(lightness, 0f);
        }

        /// <summary>
        /// Calculates the non-linear, perceived brightness of the color,
        /// using the given weights for each color channel to calculate luminance.
        /// </summary>
        public static float GetLightness(this Color color, float redFactor, float greenFactor, float blueFactor)
            => GetLightness(color.GetLuminance(redFactor,greenFactor, blueFactor));

        /// <summary>
        /// Calculates the non-linear, perceived brightness of the color,
        /// using the ITU BT.709 specification for luminance.
        /// </summary>
        public static float GetLightness_BT709(this Color color)
            => GetLightness(color.GetLuminance_BT709());

        /// <summary>
        /// Calculates the non-linear, perceived brightness of the color,
        /// using the ITU BT.601 specification for luminance.
        /// </summary>
        public static float GetLightness_BT601(this Color color)
            => GetLightness(color.GetLuminance_BT601());
        #endregion
    }
}