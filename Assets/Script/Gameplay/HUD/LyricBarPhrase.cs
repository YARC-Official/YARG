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
            public readonly double TimeLength;

            public TransitionTiming(double time, double timeEnd)
            {
                Time = time;
                TimeEnd = timeEnd;
                TimeLength = timeEnd - time;
            }
        }

        public class PhraseTransitions
        {
            public LyricsPhrase     Phrase;
            public TransitionTiming UpcomingTransition;
            public TransitionTiming ActiveTransition;
            public TransitionTiming ExitTransition;
        }

        [SerializeField]
        private TextMeshProUGUI _lyricText;

        private const float MINIMUM_TRANSITION_DURATION = 0.02f;

        private readonly Queue<PhraseTransitions> _phraseQueue = new();
        private          PhraseTransitions        _currentPhraseData;

        private readonly Vector2 _inactivePosition = new(0f, -90f);

        private readonly Vector2 _upcomingPosition = new(0f, -72f);
        private const    float   UPCOMING_ALPHA    = 0.65f;
        private readonly Vector2 _upcomingScale    = new(0.7f, 0.7f);

        private readonly Vector2 _activePosition   = new(0f, -22f);
        private readonly Vector2 _finishedPosition = new(0f, 15f);

        private int                     _currentLyricIndex;
        private Utf16ValueStringBuilder _builder;

        protected override void GameplayAwake()
        {
            _builder = ZString.CreateStringBuilder(false);
            _lyricText.alpha = 0.0f;
        }

        public void EnqueuePhrase(PhraseTransitions phrase)
        {
            _phraseQueue.Enqueue(phrase);
            if (_currentPhraseData == null)
            {
                MoveToNextPhrase();
            }
        }

        private void MoveToNextPhrase()
        {
            if (_phraseQueue.Count == 0)
            {
                gameObject.SetActive(false);
                return;
            }

            _currentPhraseData = _phraseQueue.Dequeue();
            // Reset state
            _currentLyricIndex = 0;
            // Reset visuals
            _lyricText.rectTransform.anchoredPosition = _inactivePosition;
            _lyricText.alpha = 0.0f;
            _lyricText.rectTransform.localScale = _upcomingScale;
            UpdatePhraseString();
        }

        private float CalculateTimeFraction(TransitionTiming transitionTiming)
        {
            // If the transition is REALLY short, just snap to the end to avoid weird behavior
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
            if (time >= _currentPhraseData.ExitTransition.Time)
            {
                timeFraction = CalculateTimeFraction(_currentPhraseData.ExitTransition);
                _lyricText.rectTransform.anchoredPosition = DOVirtual.EasedValue(_activePosition, _finishedPosition,
                    timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(1.0f, 0.0f, timeFraction, Ease.InOutSine);
                return;
            }

            if (time >= _currentPhraseData.ActiveTransition.Time)
            {
                timeFraction = CalculateTimeFraction(_currentPhraseData.ActiveTransition);
                _lyricText.rectTransform.anchoredPosition = DOVirtual.EasedValue(_upcomingPosition, _activePosition,
                    timeFraction, Ease.InOutSine);
                _lyricText.rectTransform.localScale = DOVirtual.EasedValue(
                    _upcomingScale, Vector3.one, timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(UPCOMING_ALPHA, 1.0f, timeFraction, Ease.InOutSine);
                return;
            }

            if (time >= _currentPhraseData.UpcomingTransition.Time)
            {
                timeFraction = CalculateTimeFraction(_currentPhraseData.UpcomingTransition);
                _lyricText.rectTransform.anchoredPosition = DOVirtual.EasedValue(_inactivePosition,
                    _upcomingPosition, timeFraction, Ease.InOutSine);
                _lyricText.alpha = DOVirtual.EasedValue(0.0f, UPCOMING_ALPHA, timeFraction, Ease.InOutSine);
            }
        }

        private void Update()
        {
            var time = GameManager.VisualTime;
            if (GameManager.VisualTime >= _currentPhraseData.ExitTransition.TimeEnd)
            {
                MoveToNextPhrase();
                return;
            }

            if (GameManager.VisualTime >= _currentPhraseData.ExitTransition.Time &&
                _currentLyricIndex != _currentPhraseData.Phrase.Lyrics.Count)
            {
                // Finish highlighting
                _currentLyricIndex = _currentPhraseData.Phrase.Lyrics.Count;
                UpdatePhraseString();
            }

            if (time >= _currentPhraseData.ActiveTransition.TimeEnd && time <= _currentPhraseData.ExitTransition.Time)
            {
                UpdateHighlighting();
            }

            UpdatePosition();
        }

        private void UpdateHighlighting()
        {
            var lyrics = _currentPhraseData.Phrase.Lyrics;
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
            var lyrics = _currentPhraseData.Phrase.Lyrics;
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