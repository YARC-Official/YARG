using System.Collections.Generic;
using Cysharp.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class LyricBarPhrase : GameplayBehaviour
    {
        public readonly struct TransitionTiming
        {
            public readonly double Time;
            public readonly double TimeEnd;
            public          double TimeLength => TimeEnd - Time;

            public TransitionTiming(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
            }
        }

        public class PhraseTransitionData
        {
            public LyricsPhrase     Phrase;
            public TransitionTiming UpcomingTransition;
            public TransitionTiming ActiveTransition;
            public TransitionTiming ExitTransition;
        }

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        private const float MINIMUM_TRANSITION_DURATION = 0.02f;

        private readonly Queue<PhraseTransitionData> _phraseQueue = new();
        private          PhraseTransitionData        _currentPhrase;

        private readonly Vector2 _inactivePosition = new(0f, -90f);

        private readonly Vector2 _upcomingPosition = new(0f, -72f);
        private readonly Vector2 _upcomingScale    = new(0.7f, 0.7f);
        private const    float   UPCOMING_ALPHA    = 0.65f;

        private readonly Vector2 _activePosition   = new(0f, -22f);
        private readonly Vector2 _finishedPosition = new(0f, 15f);

        private int                     _currentLyricIndex;
        private Utf16ValueStringBuilder _builder;
        private RectTransform           _lyricTextTransform;

        protected override void GameplayAwake()
        {
            _builder = ZString.CreateStringBuilder(false);
            // This should improve performance slightly by avoiding the need to call GetComponent
            _lyricTextTransform = _lyricText.rectTransform;
        }

        protected override void GameplayDestroy()
        {
            _builder.Dispose();
        }

        public void EnqueuePhrase(PhraseTransitionData phrase)
        {
            _phraseQueue.Enqueue(phrase);
            if (_currentPhrase == null)
            {
                MoveToNextPhrase();
            }
        }

        private void MoveToNextPhrase()
        {
            // It appears that GameManager.VisualTime cannot be used in InChartLoaded,
            // so when the first phrase is enqueued, we can't use it
            if (_currentPhrase == null)
            {
                _currentPhrase = _phraseQueue.Dequeue();
            }
            else
            {
                while (_currentPhrase.ExitTransition.TimeEnd < GameManager.VisualTime)
                {
                    if (_phraseQueue.Count == 0)
                    {
                        gameObject.SetActive(false);
                        return;
                    }

                    _currentPhrase = _phraseQueue.Dequeue();
                }
            }

            /*
             * Setting the alpha to 0, moving the text offscreen, and then updating the text before forcing a mesh update is all to try and stop the text from sometimes ghosting
             * during extremely fast lyrics. I have no idea what causes this, because what is shown on screen does not match the Unity Inspector, but they do appear to help somewhat.
             */
            _currentLyricIndex = 0;
            _lyricTextTransform.localScale = _upcomingScale;
            _lyricTextTransform.anchoredPosition = new Vector2(0, -10000f); // Just move it offscreen somewhere
            _lyricText.alpha = 0;

            UpdatePhraseString();
            _lyricText.ForceMeshUpdate(false, true);
        }

        private float CalculateTimeFraction(TransitionTiming transitionTiming)
        {
            // If the transition is REALLY short, just snap to the end to avoid weird visual behavior
            if (transitionTiming.TimeLength <= MINIMUM_TRANSITION_DURATION)
            {
                return 1.0f;
            }

            return Mathf.Clamp01((float) ((GameManager.VisualTime - transitionTiming.Time) /
                transitionTiming.TimeLength));
        }

        private void UpdatePosition()
        {
            float timeFraction;
            var time = GameManager.VisualTime;
            if (time >= _currentPhrase.ExitTransition.Time)
            {
                if (Mathf.Approximately(_lyricTextTransform.anchoredPosition.y, _finishedPosition.y))
                {
                    return;
                }

                timeFraction = CalculateTimeFraction(_currentPhrase.ExitTransition);
                _lyricTextTransform.anchoredPosition = DOVirtual.EasedValue(_activePosition, _finishedPosition,
                    timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(1.0f, 0.0f, timeFraction, Ease.InOutSine);
                return;
            }

            if (time >= _currentPhrase.ActiveTransition.Time)
            {
                if (Mathf.Approximately(_lyricTextTransform.anchoredPosition.y, _activePosition.y))
                {
                    return;
                }

                timeFraction = CalculateTimeFraction(_currentPhrase.ActiveTransition);
                _lyricTextTransform.anchoredPosition = DOVirtual.EasedValue(_upcomingPosition, _activePosition,
                    timeFraction, Ease.InOutSine);
                _lyricTextTransform.localScale = DOVirtual.EasedValue(
                    _upcomingScale, Vector3.one, timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(UPCOMING_ALPHA, 1.0f, timeFraction, Ease.InOutSine);
                return;
            }

            if (time >= _currentPhrase.UpcomingTransition.Time)
            {
                if (Mathf.Approximately(_lyricTextTransform.anchoredPosition.y, _upcomingPosition.y))
                {
                    return;
                }

                timeFraction = CalculateTimeFraction(_currentPhrase.UpcomingTransition);
                _lyricTextTransform.anchoredPosition = DOVirtual.EasedValue(_inactivePosition,
                    _upcomingPosition, timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(0.0f, UPCOMING_ALPHA, timeFraction, Ease.InOutSine);
            }
        }

        private void Update()
        {
            var time = GameManager.VisualTime;
            if (GameManager.VisualTime >= _currentPhrase.ExitTransition.TimeEnd)
            {
                MoveToNextPhrase();
            }

            if (time >= _currentPhrase.ActiveTransition.TimeEnd && time <= _currentPhrase.ExitTransition.TimeEnd)
            {
                UpdateHighlighting();
            }

            UpdatePosition();
        }

        private void UpdateHighlighting()
        {
            var lyrics = _currentPhrase.Phrase.Lyrics;
            int currentIndex = _currentLyricIndex;

            while (currentIndex < lyrics.Count && lyrics[currentIndex].Time <= GameManager.VisualTime)
            {
                currentIndex++;
            }

            if (_currentLyricIndex == currentIndex)
            {
                return;
            }

            _currentLyricIndex = currentIndex;

            UpdatePhraseString();
        }

        private void UpdatePhraseString()
        {
            var lyrics = _currentPhrase.Phrase.Lyrics;
            _builder.Clear();
            // Highlighted words
            _builder.Append("<color=#5CB9FF>");
            int i = 0;
            while (i < _currentLyricIndex)
            {
                var lyric = lyrics[i++];
                _builder.Append(lyric.Text);
                if (!lyric.JoinOrHyphenateWithNext && i < lyrics.Count)
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
                if (!lyric.JoinOrHyphenateWithNext && i < lyrics.Count)
                {
                    _builder.Append(' ');
                }
            }

            _lyricText.SetText(_builder);
        }
    }
}