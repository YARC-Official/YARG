using Cysharp.Text;
using UnityEngine;

namespace YARG.Helpers
{
    public static class TextColorer
    {
        public static string StyleString(string text, Color c, int fontWeight = 400)
        {
            string hexColor = ColorUtility.ToHtmlStringRGBA(c);
            return ZString.Format("<color=#{0}><font-weight={1}>{2}</font-weight></color>",
                hexColor, fontWeight, text);
        }
    }
}
