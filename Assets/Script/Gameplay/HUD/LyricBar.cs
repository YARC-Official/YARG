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
        private const double PHRASE_FADING = 0.5;
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

        private List<LyricsPhrase> _phrases;
        private bool _upcomingLineSet = false;

        private int _currentPhraseIndex = 0;
        private int _currentLyricIndex = -1;
        private Utf16ValueStringBuilder _builder;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

            if (GameManager.IsPractice || lyricSetting == LyricDisplayMode.Disabled)
            {
                gameObject.SetActive(false);
                return;
            }

            _builder = ZString.CreateStringBuilder(false);
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
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _phrases = chart.Lyrics.Phrases;
            if (_phrases.Count < 1)
            {
                gameObject.SetActive(false);
            }
        }

        protected override void GameplayDestroy()
        {
            if (!GameManager.IsPractice && SettingsManager.Settings.LyricDisplay.Value != LyricDisplayMode.Disabled)
            {
                _builder.Dispose();
                return;
            }
        }

        private void Update()
        {
            while (_currentPhraseIndex < _phrases.Count && _phrases[_currentPhraseIndex].TimeEnd <= GameManager.SongTime)
            {
                // We don't want to immedately remove the current line if the next one is close enough
                if (_currentPhraseIndex + 1 == _phrases.Count || _phrases[_currentPhraseIndex + 1].Time - _phrases[_currentPhraseIndex].TimeEnd >= PHRASE_DISTANCE_THRESHOLD)
                {
                    double fadeOut = GameManager.SongTime - _phrases[_currentPhraseIndex].TimeEnd;
                    if (fadeOut < PHRASE_FADING)
                    {
                        float alpha = 1 - (float) (fadeOut / PHRASE_FADING);
                        _lyricText.alpha = alpha;
                        _upcomingLyricText.alpha = alpha;
                        break;
                    }
                }
                else if (GameManager.SongTime < _phrases[_currentPhraseIndex + 1].Time)
                {
                    break;
                }

                _currentPhraseIndex++;
                _currentLyricIndex = -1;
                _lyricText.text = string.Empty;
                _upcomingLyricText.text = string.Empty;
                _upcomingLineSet = false;
            }

            // Exit if we've complete all phrases
            if (_currentPhraseIndex == _phrases.Count)
            {
                return;
            }

            if (GameManager.SongTime < _phrases[_currentPhraseIndex].Time)
            {
                double fadeIn = _phrases[_currentPhraseIndex].Time - GameManager.SongTime;
                if (fadeIn >= PHRASE_FADING)
                {
                    return;
                }
                float alpha = 1 - (float) (fadeIn / PHRASE_FADING);
                _lyricText.alpha = alpha;
                _upcomingLyricText.alpha = alpha;
            }
            // Fade-out could be occuring, so we can't just always set alpha to 1.0f here
            else if (GameManager.SongTime < _phrases[_currentPhraseIndex].TimeEnd)
            {
                _lyricText.alpha = 1;
                _upcomingLyricText.alpha = 1;
            }

            UpdateCurrentPhrase();
            UpdateUpcomingPhrase();
        }

        private void UpdateCurrentPhrase()
        {
            var lyrics = _phrases[_currentPhraseIndex].Lyrics;

            // Update the lyric index
            int currIndex = _currentLyricIndex;
            while (currIndex == -1 || (currIndex < lyrics.Count && lyrics[currIndex].Time <= GameManager.SongTime))
            {
                currIndex++;
            }

            // If the lyric index hasn't changed, then skip
            if (_currentLyricIndex == currIndex)
            {
                return;
            }

            _builder.Clear();
            // Highlighted words
            _builder.Append("<color=#5CB9FF>");
            int i = 0;
            while (i < currIndex)
            {
                var lyric = lyrics[i++];
                _builder.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    _builder.Append(' ');
                }
            }
            _builder.Append("</color>");

            // Non-highlighted words
            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                _builder.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    _builder.Append(' ');
                }
            }

            _currentLyricIndex = currIndex;
            _lyricText.SetText(_builder);
        }

        private void UpdateUpcomingPhrase()
        {
            const double MIN_PHRASE_LENGTH = 0.1;
            if (_upcomingLineSet)
            {
                return;
            }

            _upcomingLineSet = true;
            // We only want the upcoming phrase show if the phrase starts within the phrase-to-phrase threshold.
            // We also give an excpetion for very very very short phrases (usually for special effects).
            if (_currentPhraseIndex + 1 == _phrases.Count
            || _phrases[_currentPhraseIndex + 1].Time - _phrases[_currentPhraseIndex].TimeEnd >= PHRASE_DISTANCE_THRESHOLD
            || _phrases[_currentPhraseIndex].TimeLength < MIN_PHRASE_LENGTH)
            {
                return;
            }

            var lyrics = _phrases[_currentPhraseIndex + 1].Lyrics;

            _builder.Clear();
            int i = 0;
            while (i < lyrics.Count)
            {
                var lyric = lyrics[i++];
                _builder.Append(lyric.Text);
                if (!lyric.JoinWithNext && i < lyrics.Count)
                {
                    _builder.Append(' ');
                }
            }

            _upcomingLyricText.SetText(_builder);
        }
    }
}
