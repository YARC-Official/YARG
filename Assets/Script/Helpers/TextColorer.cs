using UnityEngine;

namespace YARG.Helpers
{
    public static class TextColorer
    {
        public static string FormatString(string text, Color c, int fontWeight = 400)
        {
            string hexColor = "#" + ColorUtility.ToHtmlStringRGBA(c);
            return $"<color={hexColor}><font-weight={fontWeight}>{text}</font-weight></color>";
        }
    }
}
