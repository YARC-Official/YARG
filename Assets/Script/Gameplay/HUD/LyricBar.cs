using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum LyricDisplayMode
    {
        Disabled,
        Normal,
        Transparent,
        NoBackground,
    }

    public class LyricBar : GameplayBehaviour
    {
        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _transparentBackground;

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        private LyricsTrack _lyrics;
        private int _currentPhraseIndex = 0;
        private int _currentLyricIndex = 0;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

            if (GameManager.IsPractice || lyricSetting == LyricDisplayMode.Disabled)
            {
                gameObject.SetActive(false);
                return;
            }

            // Set the lyric background
            switch (lyricSetting)
            {
                case LyricDisplayMode.Normal:
                    _normalBackground.SetActive(true);
                    _transparentBackground.SetActive(false);
                    break;
                case LyricDisplayMode.Transparent:
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(true);
                    break;
                case LyricDisplayMode.NoBackground:
                    _normalBackground.SetActive(false);
                    _transparentBackground.SetActive(false);
                    break;
            }

            // Reset the lyrics
            _lyricText.text = string.Empty;
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _lyrics = chart.Lyrics;
            if (_lyrics.Phrases.Count < 1)
                gameObject.SetActive(false);
        }

        private void Update()
        {
            const double PHRASE_DISTANCE_THRESHOLD = 1.0;

            var phrases = _lyrics.Phrases;

            // If the current phrase ended AND
            while (_currentPhraseIndex < phrases.Count && phrases[_currentPhraseIndex].TimeEnd <= GameManager.SongTime &&
                 // Was the last phrase
                (_currentPhraseIndex + 1 == phrases.Count ||
                 // OR if the next phrase is one second or more away (leading to an empty bar)
                 phrases[_currentPhraseIndex + 1].Time - phrases[_currentPhraseIndex].TimeEnd >= PHRASE_DISTANCE_THRESHOLD ||
                 // OR if the next phrase should be started
                 phrases[_currentPhraseIndex + 1].Time <= GameManager.SongTime))
            {
                _currentPhraseIndex++;
                _currentLyricIndex = 0;
                _lyricText.text = null;
            }

            if (_currentPhraseIndex == phrases.Count || GameManager.SongTime < phrases[_currentPhraseIndex].Time)
                return;

            var lyrics = phrases[_currentPhraseIndex].Lyrics;

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
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                    output.Append(' ');
            }

            // End highlight
            output.Append("</color>");

            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                    output.Append(' ');
            }

            _currentLyricIndex = currIndex;
            _lyricText.text = output.ToString();
        }
    }
}
