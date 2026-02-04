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

        private readonly List<double> _fadeTimings = new();
        private          int _fadeIndex;
        private const    int PHRASE_OBJECT_COUNT = 5;
        private const    double PHRASE_DISTANCE_THRESHOLD = 2.0; // At least 2 * FADE_DURATION, to allow for fade in/out
        private const    double MAX_TRANSITION_DURATION = 0.3;
        private const    double FADE_DURATION = 0.5;

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
            var lyrics = chart.Lyrics;

            if (lyrics.Phrases.Count < 1)
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

            var phrases = lyrics.Phrases;
            LyricBarPhrase.LyricPhraseTimingData previousPhrase = null;
            for (int i = 0; i < phrases.Count; i++)
            {
                var currentPhrase = phrases[i];
                int phraseObjectIndex = i % PHRASE_OBJECT_COUNT;
                var phraseData = new LyricBarPhrase.LyricPhraseTimingData
                {
                    Phrase = currentPhrase,
                };
                if (i == 0)
                {
                    // Create object for first phrase with only fade in
                    double initialFadeInTime = currentPhrase.Time - FADE_DURATION;
                    phraseData.UpcomingTransition =
                        new LyricBarPhrase.TransitionTiming(initialFadeInTime, initialFadeInTime);
                    phraseData.ActiveTransition =
                        new LyricBarPhrase.TransitionTiming(initialFadeInTime, initialFadeInTime);
                    //phraseTimings.Add(phraseData);
                    previousPhrase = phraseData;
                    phraseObjects[phraseObjectIndex].EnqueuePhrase(phraseData);
                    // Also add fade in
                    _fadeTimings.Add(initialFadeInTime);
                    continue;
                }

                if (i == phrases.Count - 1)
                {
                    // Last phrase, so set exit transition to fade out
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
                    // This is a fade in/out, so we set transitions to 0
                    double timeBeforeFadeIn = currentPhrase.Time - FADE_DURATION;
                    double timeAfterFadeOut = previousPhrase.Phrase.TimeEnd + FADE_DURATION;
                    phraseData.ActiveTransition =
                        new LyricBarPhrase.TransitionTiming(timeBeforeFadeIn, timeBeforeFadeIn);
                    phraseData.UpcomingTransition =
                        new LyricBarPhrase.TransitionTiming(timeBeforeFadeIn, timeBeforeFadeIn);
                    previousPhrase.ExitTransition =
                        new LyricBarPhrase.TransitionTiming(timeAfterFadeOut, timeAfterFadeOut);
                    _fadeTimings.Add(timeAfterFadeOut - FADE_DURATION);
                    _fadeTimings.Add(timeBeforeFadeIn);
                }
                else
                {
                    // This needs to be different from phraseGap, since often the end of the previous phrase = the start of the current one
                    // And therefore phraseGap = 0, which is not helpful for determining transition times
                    double distanceFromLastLyric = currentPhrase.Time -
                        previousPhrase.Phrase.Lyrics.Last().Time;
                    double mainTransitionTime = Math.Min(distanceFromLastLyric, MAX_TRANSITION_DURATION);
                    phraseData.ActiveTransition = new LyricBarPhrase.TransitionTiming(
                        currentPhrase.Time - mainTransitionTime,
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

            YargLogger.LogFormatDebug("Fading lyrics at time {0}.",
                GameManager.VisualTime);
            var startValue = _fadeIndex % 2 == 0 ? 0f : 1f;
            var targetValue = _fadeIndex % 2 == 0 ? 1f : 0f;
            var timeFraction = Mathf.Clamp01((float) (1 - (fadeTime + FADE_DURATION - GameManager.VisualTime) /
                FADE_DURATION));
            _canvas.alpha = DOVirtual.EasedValue(startValue, targetValue, timeFraction, Ease.InOutSine);
            if (GameManager.VisualTime >= fadeTime + FADE_DURATION)
            {
                _fadeIndex++;
            }
        }
    }
}