using System.Collections.Generic;
using System.Text;
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
        private int _currentPhraseIndex = 0;
        private int _currentLyricIndex = 0;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

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
            const double PHRASE_DISTANCE_THRESHOLD = 1.0;
            // If the current phrase ended AND
            while (_currentPhraseIndex < _vocalPhrases.Count && _vocalPhrases[_currentPhraseIndex].TimeEnd <= GameManager.SongTime &&
                 // Was the last phrase
                (_currentPhraseIndex + 1 == _vocalPhrases.Count ||
                 // OR if the next phrase is one second or more away (leading to an empty bar)
                 _vocalPhrases[_currentPhraseIndex + 1].Time - _vocalPhrases[_currentPhraseIndex].TimeEnd >= PHRASE_DISTANCE_THRESHOLD ||
                 // OR if the next phrase should be started
                 _vocalPhrases[_currentPhraseIndex + 1].Time <= GameManager.SongTime))
            {
                _currentPhraseIndex++;
                _currentLyricIndex = 0;
                _lyricText.text = null;
            }

            if (_currentPhraseIndex == _vocalPhrases.Count || GameManager.SongTime < _vocalPhrases[_currentPhraseIndex].Time)
                return;

            var lyrics = _vocalPhrases[_currentPhraseIndex].Lyrics;

            // Check following lyrics
            int currIndex = _currentLyricIndex;
            while(currIndex < lyrics.Count && lyrics[currIndex].Time <= GameManager.SongTime)
                currIndex++;

            // No update necessary
            if (_lyricText.text != null && _currentLyricIndex == currIndex)
                return;

            // Construct lyrics to be displayed
            // Start highlight
            StringBuilder output = new("<color=#5CB9FF>");
            int i = 0;
            while (i < currIndex)
            {
                var lyric = lyrics[i++];
                output.Append(GetDisplayTextWithSpace(lyric.Text));
            }

            // End highlight
            output.Append("</color>");

            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                output.Append(GetDisplayTextWithSpace(lyric.Text));
            }

            _currentLyricIndex = currIndex;
            _lyricText.text = output.ToString();
        }

        private string GetDisplayTextWithSpace(string lyricText)
        {
            // Get rid of extra spaces
            lyricText = lyricText.Trim();

            // If this is a connector, just return nothing
            if (lyricText == "+")
            {
                return string.Empty;
            }

            // Special replacements
            lyricText = lyricText.Replace('=', '-');
            lyricText = lyricText.Replace('_', ' ');
            lyricText = lyricText.Replace('§', '‿');

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
