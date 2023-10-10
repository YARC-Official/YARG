using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.Visuals
{
    public class VocalLyricElement : VocalElement
    {
        private static readonly Regex _lyricDiacriticRegex = new(@"\+|#|\^|\*|\%|\$|\/|", RegexOptions.Compiled);

        public TextEvent LyricRef { get; set; }

        protected override double ElementTime => LyricRef.Time;

        [SerializeField]
        private TextMeshPro _lyricText;

        protected override void InitializeElement()
        {
            _lyricText.text = GetLyricText(LyricRef.Text);

            // Disable automatically if the text is just nothing
            if (string.IsNullOrEmpty(_lyricText.text))
            {
                DisableIntoPool();
            }
        }

        protected override void UpdateElement()
        {
        }

        protected override void HideElement()
        {
        }

        private static string GetLyricText(string lyricText)
        {
            // Get rid of extra spaces
            lyricText = lyricText.Trim();

            // Special replacements
            lyricText = lyricText.Replace('=', '-');
            lyricText = lyricText.Replace('_', ' ');
            lyricText = lyricText.Replace('§', '\u203F');

            // Remove all other diacritics
            return _lyricDiacriticRegex.Replace(lyricText, "");
        }
    }
}