using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class LyricBar : GameplayBehaviour
    {
        private static readonly Regex _lyricDiacriticRegex = new(@"#|\^|\*|\%|\$|\/|", RegexOptions.Compiled);

        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _transparentBackground;

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        private List<VocalsPhrase> _vocalPhrases;
        private int _currentPhraseIndex = -1;
        private int _currentLyricIndex = -1;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Data;

            if (GameManager.IsPractice || lyricSetting == "NoLyricDisplay")
            {
                gameObject.SetActive(false);
                return;
            }

            // Set the lyric background
            switch (lyricSetting)
            {
                case "Normal":
                    _normalBackground.SetActive(true);
                    _transparentBackground.SetActive(false);
                    break;
                case "Transparent":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(true);
                    break;
                case "NoBackground":
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(false);
                    break;
            }

            // Reset the lyrics
            _lyricText.text = string.Empty;
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            var vocalsPart = chart.Vocals.Parts[0];

            // If not vocals, hide the lyric bar
            if (vocalsPart.NotePhrases.Count <= 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _vocalPhrases = vocalsPart.NotePhrases;
        }

        private void Update()
        {
            // Wait until the chart loads
            if (_vocalPhrases is null) return;

            // Go to the next phrase if it has started
            if (_currentPhraseIndex + 1 < _vocalPhrases.Count && _vocalPhrases[_currentPhraseIndex + 1].Time <= GameManager.SongTime)
            {
                _currentPhraseIndex++;
                _currentLyricIndex = -1;
            }

            // Skip if there hasn't been any vocals yet, or the song is done
            if (_currentPhraseIndex == -1 || _currentPhraseIndex >= _vocalPhrases.Count) return;

            var lyrics = _vocalPhrases[_currentPhraseIndex].Lyrics;

            // Check for next lyric
            if (_currentLyricIndex + 1 < lyrics.Count &&
                lyrics[_currentLyricIndex + 1].Time <= GameManager.SongTime)
            {
                _currentLyricIndex++;
            }

            // Get what should be displayed and show it
            string output = "<color=#5CB9FF>";
            for (int i = 0; i < lyrics.Count; i++)
            {
                // End highlight here
                if (i == _currentLyricIndex + 1)
                {
                    output += "</color>";
                }

                var lyric = lyrics[i];
                output += GetDisplayTextWithSpace(lyric.Text);
            }

            _lyricText.text = output;
        }

        private string GetDisplayTextWithSpace(string lyricText)
        {
            // Get rid of extra spaces
            lyricText = lyricText.Trim();

            // If this is a connector, just return nothing
            if (lyricText == "+")
            {
                return "";
            }

            // Special replacements
            lyricText = lyricText.Replace('=', '-');
            lyricText = lyricText.Replace('_', ' ');
            lyricText = lyricText.Replace('§', '\u203F');

            // Remove all other diacritics
            lyricText = _lyricDiacriticRegex.Replace(lyricText, "");

            if (lyricText.EndsWith('-'))
            {
                // If this is a syllable, return with the dash removed
                return lyricText[..^1];
            }

            // If not, just return it with a space
            return lyricText + " ";
        }
    }
}
