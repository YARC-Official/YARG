using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class ColorExtensions
    {

        public static Color ToUnityColor(this System.Drawing.Color color)
        {
            return new Color(color.R / 255f, color.G / 255f, color.B / 255f, color.A / 255f);
        }

    }
}