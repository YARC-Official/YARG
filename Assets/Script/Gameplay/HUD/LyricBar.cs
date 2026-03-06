using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Core.Logging;
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
        private LyricBarPhrase _phrasePrefab;
        [SerializeField]
        private CanvasGroup _canvas;

        private readonly List<double>     _fadeStartTimings = new();
        private          int              _fadeIndex;
        private          LyricBarPhrase[] _lyricTextObjects;
        private          SongChart        _songChart;

        private const int    PHRASE_OBJECT_COUNT       = 3;
        private const double PHRASE_DISTANCE_THRESHOLD = 2.0; // At least 2 * FADE_DURATION, to allow for fade in/out
        private const double MAX_TRANSITION_DURATION   = 0.3;
        private const double FADE_DURATION             = 0.5;

        private bool ShouldEnable =>
            !(GameManager.IsPractice || SettingsManager.Settings.LyricDisplay.Value == LyricDisplayMode.Disabled);

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

            if (!ShouldEnable)
            {
                gameObject.SetActive(false);
                return;
            }

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
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _songChart = chart;
            if (_songChart.Lyrics.Phrases.Count < 1)
            {
                gameObject.SetActive(false);
                return;
            }

            if (GameManager.IsPractice || SettingsManager.Settings.LyricDisplay.Value == LyricDisplayMode.Disabled)
            {
                // The object is already disabled in GameplayAwake, but that doesn't stop OnChartLoaded from being called
                return;
            }

            _lyricTextObjects = new LyricBarPhrase[PHRASE_OBJECT_COUNT];
            for (int i = 0; i < PHRASE_OBJECT_COUNT; i++)
            {
                var phraseObject = Instantiate(_phrasePrefab, _canvas.transform);
                _lyricTextObjects[i] = phraseObject;
            }

            BuildLyricTimings();
        }

        private void BuildLyricTimings()
        {
            var phrases = _songChart.Lyrics.Phrases;
            LyricBarPhrase.PhraseTransitionData previousPhraseTransitionData = null;
            for (int i = 0; i < phrases.Count; i++)
            {
                var currentPhrase = phrases[i];
                int textObjectIndex = i % PHRASE_OBJECT_COUNT;

                var phraseData = new LyricBarPhrase.PhraseTransitionData
                {
                    Phrase = currentPhrase,
                };

                if (i == 0)
                {
                    // First phrase fades in
                    double initialFadeInTime = currentPhrase.Time - FADE_DURATION;
                    phraseData.UpcomingTransition =
                        new LyricBarPhrase.TransitionTiming(initialFadeInTime, initialFadeInTime);
                    phraseData.ActiveTransition =
                        new LyricBarPhrase.TransitionTiming(initialFadeInTime, initialFadeInTime);
                    previousPhraseTransitionData = phraseData;
                    _lyricTextObjects[textObjectIndex].AddPhrase(phraseData);
                    _fadeStartTimings.Add(initialFadeInTime);
                    continue;
                }

                /*
                 * Ignoring fade-ins and fade-outs, it should work like such:
                 * A phrase's upcoming transition should be the same length as the previous phrase's main transition.
                 * A previous phrase's exit transition should be the same length as the current phrase's main transition.
                 * A phrase's transition to Main should be as large as possible up to MAXIMUM_TRANSITION_TIME,
                 *  but otherwise equal to the distance from the end of the previous phrase's last lyric.
                 */

                double phraseGap = currentPhrase.Time - previousPhraseTransitionData!.Phrase.TimeEnd;
                if (phraseGap > PHRASE_DISTANCE_THRESHOLD)
                {
                    double fadeInTime = currentPhrase.Time - FADE_DURATION;
                    double fadeOutTime = previousPhraseTransitionData.Phrase.TimeEnd;
                    // This is a fade in/out, so transitions should be instant, and occur when the lyric bar is fully faded out.
                    phraseData.ActiveTransition =
                        new LyricBarPhrase.TransitionTiming(fadeInTime, fadeInTime);
                    phraseData.UpcomingTransition =
                        new LyricBarPhrase.TransitionTiming(fadeInTime, fadeInTime);
                    previousPhraseTransitionData.ExitTransition =
                        new LyricBarPhrase.TransitionTiming(fadeOutTime + FADE_DURATION, fadeOutTime + FADE_DURATION);
                    _fadeStartTimings.Add(fadeOutTime);
                    _fadeStartTimings.Add(fadeInTime);
                }
                else
                {
                    // distanceFromLastLyric needs to be different from phraseGap, since often the end of the previous phrase = the start of the current one
                    // And therefore phraseGap = 0, which is not helpful for determining transition times
                    // And the fades look weird if you were to use distanceFromLastLyric for them, since, especially for .charts, lyrics have no length.
                    double distanceFromLastLyric = currentPhrase.Time -
                        previousPhraseTransitionData.Phrase.Lyrics.Last().TimeEnd;
                    double activeTransitionTime = Math.Min(distanceFromLastLyric, MAX_TRANSITION_DURATION);
                    phraseData.ActiveTransition = new LyricBarPhrase.TransitionTiming(
                        currentPhrase.Time - activeTransitionTime,
                        currentPhrase.Time);
                    phraseData.UpcomingTransition = new LyricBarPhrase.TransitionTiming(
                        previousPhraseTransitionData.ActiveTransition.Time,
                        previousPhraseTransitionData.ActiveTransition.TimeEnd);
                    previousPhraseTransitionData.ExitTransition = new LyricBarPhrase.TransitionTiming(
                        phraseData.ActiveTransition.Time,
                        phraseData.ActiveTransition.TimeEnd);
                }

                if (i == phrases.Count - 1)
                {
                    // Last phrase fades out
                    phraseData.ExitTransition =
                        new LyricBarPhrase.TransitionTiming(currentPhrase.TimeEnd + FADE_DURATION,
                            currentPhrase.TimeEnd + FADE_DURATION);
                    _fadeStartTimings.Add(currentPhrase.TimeEnd);
                }

                previousPhraseTransitionData = phraseData;
                _lyricTextObjects[textObjectIndex].AddPhrase(phraseData);
            }
        }

        public void SetSongTime(double time)
        {
            if (!ShouldEnable)
            {
                return;
            }

            // In case we are disabled already
            enabled = true;

            _fadeIndex = 0;

            // Tell LyricBarPhrase about the new time
            foreach (var lyricTextObject in _lyricTextObjects)
            {
                lyricTextObject.gameObject.SetActive(true);
                lyricTextObject.SetSongTime(time);
            }

            // set the fade start index
            while (_fadeStartTimings.Count > 0 && _fadeIndex < _fadeStartTimings.Count &&
                _fadeStartTimings[_fadeIndex] < time + FADE_DURATION)
            {
                _fadeIndex++;
            }

            if (_fadeIndex > _fadeStartTimings.Count - 1)
            {
                enabled = false;
                return;
            }

            // We don't want to call SetAlpha when we are not in a fade in Update for performance,
            // so call it once here to set the alpha to whatever it should be at the current time
            SetAlpha(_fadeStartTimings[_fadeIndex]);
        }

        private void SetAlpha(double fadeStartTime)
        {
            var startValue = _fadeIndex % 2 == 0 ? 0f : 1f;
            var targetValue = _fadeIndex % 2 == 0 ? 1f : 0f;
            var progress = Mathf.Clamp01((float) (1 - (fadeStartTime + FADE_DURATION - GameManager.VisualTime) /
                FADE_DURATION));
            _canvas.alpha = DOVirtual.EasedValue(startValue, targetValue, progress, Ease.InOutSine);
        }

        private void Update()
        {
            var fadeStartTime = _fadeStartTimings[_fadeIndex];

            if (GameManager.VisualTime <= fadeStartTime)
            {
                return;
            }

            SetAlpha(fadeStartTime);

            if (GameManager.VisualTime < fadeStartTime + FADE_DURATION)
            {
                return;
            }

            YargLogger.LogFormatTrace("Lyric bar fade {0} complete at time {1}", _fadeIndex,
                GameManager.VisualTime);
            if (_fadeIndex == _fadeStartTimings.Count - 1)
            {
                // No more fades, lyric bar is done
                enabled = false;
                return;
            }

            _fadeIndex++;
        }
    }
}