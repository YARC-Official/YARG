using System.Collections.Generic;
using Cysharp.Text;
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
        private const double PHRASE_START_PADDING = 0.5;
        private const double PHRASE_DISTANCE_THRESHOLD = 2.0;

        [SerializeField]
        private GameObject _normalBackground;
        [SerializeField]
        private GameObject _transparentBackground;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _lyricText;
        [SerializeField]
        private TextMeshProUGUI _upcomingLyricText;

        private LyricsTrack _lyrics;

        private double _upcomingLyricsThreshold;
        private bool _upcomingLyricsSet;

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
            _upcomingLyricText.text = string.Empty;

            _upcomingLyricsThreshold = SettingsManager.Settings.UpcomingLyricsTime.Value;
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _lyrics = chart.Lyrics;
            if (_lyrics.Phrases.Count < 1)
            {
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
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
                _currentLyricIndex = -1;

                _lyricText.text = string.Empty;

                _upcomingLyricText.text = string.Empty;
                _upcomingLyricsSet = false;
            }

            // Exit if we've complete all phrases
            if (_currentPhraseIndex == _lyrics.Phrases.Count)
            {
                return;
            }

            var lyrics = phrases[_currentPhraseIndex].Lyrics;

            // If it's not time to show the lyrics...
            if (GameManager.SongTime < phrases[_currentPhraseIndex].Time - PHRASE_START_PADDING)
            {
                // Add it to the upcoming line, if the timing is right
                if (!_upcomingLyricsSet &&
                    GameManager.SongTime >= phrases[_currentPhraseIndex].Time - _upcomingLyricsThreshold)
                {
                    SetUpcomingLyrics(lyrics);
                }

                // Exit
                return;
            }

            // At this point, if the current lyric is being displayed (which happens below),
            // then update the upcoming one (if it's not the last).
            if (_currentPhraseIndex + 1 < phrases.Count)
            {
                var nextPhrase = phrases[_currentPhraseIndex + 1];
                if (GameManager.SongTime >= nextPhrase.Time - _upcomingLyricsThreshold)
                {
                    if (!_upcomingLyricsSet)
                    {
                        SetUpcomingLyrics(nextPhrase.Lyrics);
                    }
                }
                else
                {
                    _upcomingLyricText.text = string.Empty;
                    _upcomingLyricsSet = false;
                }
            }
            else
            {
                _upcomingLyricText.text = string.Empty;
                _upcomingLyricsSet = false;
            }

            // Update the lyric index
            int currIndex = _currentLyricIndex;
            while (currIndex == -1 ||
                (currIndex < lyrics.Count && lyrics[currIndex].Time <= GameManager.SongTime))
            {
                currIndex++;
            }

            // If the lyric index hasn't changed, then skip
            if (_currentLyricIndex == currIndex)
            {
                return;
            }

            // Construct lyrics to be displayed
            using var output = ZString.CreateStringBuilder(true);

            // Highlighted words
            output.Append("<color=#5CB9FF>");
            int i = 0;
            while (i < currIndex)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    output.Append(' ');
                }
            }
            output.Append("</color>");

            // Non-highlighted words
            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    output.Append(' ');
                }
            }

            _currentLyricIndex = currIndex;
            _lyricText.SetText(output);
        }

        private void SetUpcomingLyrics(IReadOnlyList<LyricEvent> lyrics)
        {
            using var output = ZString.CreateStringBuilder(false);

            int i = 0;
            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                output.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    output.Append(' ');
                }
            }

            _upcomingLyricText.SetText(output);
            _upcomingLyricsSet = true;
        }
    }
}
