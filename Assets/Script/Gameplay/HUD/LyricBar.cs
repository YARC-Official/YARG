using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
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
        private LyricBarPhrase _phrasePrefab;
        [SerializeField]
        private CanvasGroup _canvas;

        private readonly List<double> _fadeTimings = new();
        private          int          _fadeIndex;

        private const int    PHRASE_OBJECT_COUNT       = 3;
        private const double PHRASE_DISTANCE_THRESHOLD = 2.0; // At least 2 * FADE_DURATION, to allow for fade in/out
        private const double MAX_TRANSITION_DURATION   = 0.3;
        private const double FADE_DURATION             = 0.5;

        protected override void GameplayAwake()
        {
            var lyricSetting = SettingsManager.Settings.LyricDisplay.Value;

            if (GameManager.IsPractice || lyricSetting == LyricDisplayMode.Disabled)
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
            if (chart.Lyrics.Phrases.Count < 1)
            {
                gameObject.SetActive(false);
                return;
            }

            var phraseObjects = new LyricBarPhrase[PHRASE_OBJECT_COUNT];
            for (int i = 0; i < PHRASE_OBJECT_COUNT; i++)
            {
                var phraseObject = Instantiate(_phrasePrefab, _canvas.transform);
                phraseObjects[i] = phraseObject;
            }

            var phrases = chart.Lyrics.Phrases;
            LyricBarPhrase.PhraseTransitions previousPhrase = null;
            for (int i = 0; i < phrases.Count; i++)
            {
                var currentPhrase = phrases[i];
                int phraseObjectIndex = i % PHRASE_OBJECT_COUNT;
                var phraseData = new LyricBarPhrase.PhraseTransitions
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
                    previousPhrase = phraseData;
                    phraseObjects[phraseObjectIndex].EnqueuePhrase(phraseData);
                    _fadeTimings.Add(initialFadeInTime);
                    continue;
                }

                if (i == phrases.Count - 1)
                {
                    // Last phrase fades out
                    phraseData.ExitTransition =
                        new LyricBarPhrase.TransitionTiming(currentPhrase.TimeEnd + FADE_DURATION,
                            currentPhrase.TimeEnd + FADE_DURATION);
                    _fadeTimings.Add(currentPhrase.TimeEnd);
                }

                /*
                 * Ignoring fade-ins and fade-outs, it should work like such:
                 * A phrase's transition to Next should be the same length as the previous phrase's transition to Main.
                 * A previous phrase's transition to Exit should be the same length as the current phrase's transition to Main.
                 * A phrase's transition to Main should be as large as possible up to MAXIMUM_TRANSITION_TIME,
                 *  but otherwise equal to the distance from the end of the previous phrase's last lyric.
                 */

                double phraseGap = currentPhrase.Time - previousPhrase!.Phrase.TimeEnd;
                if (phraseGap > PHRASE_DISTANCE_THRESHOLD)
                {
                    double fadeInTime = currentPhrase.Time - FADE_DURATION;
                    double fadeOutTime = previousPhrase.Phrase.TimeEnd;
                    // This is a fade in/out, so transitions should be instant, and occur when the lyric bar is fully faded out.
                    phraseData.ActiveTransition =
                        new LyricBarPhrase.TransitionTiming(fadeInTime, fadeInTime);
                    phraseData.UpcomingTransition =
                        new LyricBarPhrase.TransitionTiming(fadeInTime, fadeInTime);
                    previousPhrase.ExitTransition =
                        new LyricBarPhrase.TransitionTiming(fadeOutTime + FADE_DURATION, fadeOutTime + FADE_DURATION);
                    _fadeTimings.Add(fadeOutTime);
                    _fadeTimings.Add(fadeInTime);
                }
                else
                {
                    // distanceFromLastLyric needs to be different from phraseGap, since often the end of the previous phrase = the start of the current one
                    // And therefore phraseGap = 0, which is not helpful for determining transition times
                    double distanceFromLastLyric = currentPhrase.Time -
                        previousPhrase.Phrase.Lyrics.Last().TimeEnd;
                    double activeTransitionTime = Math.Min(distanceFromLastLyric, MAX_TRANSITION_DURATION);
                    phraseData.ActiveTransition = new LyricBarPhrase.TransitionTiming(
                        currentPhrase.Time - activeTransitionTime,
                        currentPhrase.Time);
                    phraseData.UpcomingTransition = new LyricBarPhrase.TransitionTiming(
                        previousPhrase.ActiveTransition.Time,
                        previousPhrase.ActiveTransition.TimeEnd);
                    previousPhrase.ExitTransition = new LyricBarPhrase.TransitionTiming(
                        phraseData.ActiveTransition.Time,
                        phraseData.ActiveTransition.TimeEnd);
                }

                previousPhrase = phraseData;
                phraseObjects[phraseObjectIndex].EnqueuePhrase(phraseData);
            }
        }

        private void Update()
        {
            var fadeTime = _fadeTimings[_fadeIndex];
            if (GameManager.VisualTime < fadeTime)
            {
                return;
            }

            var startValue = _fadeIndex % 2 == 0 ? 0f : 1f;
            var targetValue = _fadeIndex % 2 == 0 ? 1f : 0f;
            var progress = Mathf.Clamp01((float) (1 - (fadeTime + FADE_DURATION - GameManager.VisualTime) /
                FADE_DURATION));
            _canvas.alpha = DOVirtual.EasedValue(startValue, targetValue, progress, Ease.InOutSine);
            if (GameManager.VisualTime >= fadeTime + FADE_DURATION)
            {
                if (_fadeIndex == _fadeTimings.Count - 1)
                {
                    // No more fades, lyric bar is done
                    enabled = false;
                    return;
                }

                _fadeIndex++;
            }
        }
    }
}