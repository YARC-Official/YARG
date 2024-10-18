using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using System;
using YARG.Core.Logging;

namespace YARG.Gameplay.HUD
{
    public enum CountdownDisplayMode
    {
        Disabled,
        Measures,
        Seconds
    }

    public class CountdownDisplay : GameplayBehaviour
    {
        private const float FADE_ANIM_LENGTH = 0.5f;

        public static CountdownDisplayMode DisplayStyle;

        [SerializeField]
        private Image _backgroundCircle;
        [SerializeField]
        private TextMeshProUGUI _countdownText;
        [SerializeField]
        private Image _progressBar;

        [Space]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        private Coroutine _currentCoroutine;

        private bool _displayActive;

        private double _nextMeasureTime = -1;

        public void UpdateCountdown(double countdownLength, double endTime)
        {
            if (DisplayStyle == CountdownDisplayMode.Disabled)
            {
                return;
            }

            double currentTime = GameManager.SongTime;
            double timeRemaining = endTime - currentTime;

            bool shouldDisplay = timeRemaining > WaitCountdown.END_COUNTDOWN_SECOND + FADE_ANIM_LENGTH;

            if (GameManager.IsPractice)
            {
                double sectionStartTime = GameManager.PracticeManager.TimeStart;
                if (currentTime <= sectionStartTime)
                {
                    // Do not show a countdown before the start of a practice section
                    // where all of the notes before that section are removed for practice stats
                    shouldDisplay = false;
                }
            }

            ToggleDisplay(shouldDisplay);
            
            if (!gameObject.activeSelf)
            {
                return;
            }

            string displayNumber = DisplayStyle switch
            {
                CountdownDisplayMode.Measures => GetMeasuresLeft(endTime),
                CountdownDisplayMode.Seconds => Math.Ceiling(timeRemaining).ToString(),
                _ => throw new Exception("Unreachable")
            };

            _countdownText.text = displayNumber;
            
            _progressBar.fillAmount = (float) (timeRemaining / countdownLength);
        }

        public void ForceReset()
        {
            StopCurrentCoroutine();

            _canvasGroup.alpha = 0f;
            gameObject.SetActive(true);
            _displayActive = false;
        }

        private void ToggleDisplay(bool isActive)
        {
            if (isActive == _displayActive)
            {
                return;
            }

            _displayActive = isActive;

            StopCurrentCoroutine();

            if (isActive)
            {
                _canvasGroup.alpha = 0f;
                gameObject.SetActive(true);

                _currentCoroutine = StartCoroutine(ShowCoroutine());
            }
            else
            {
                if (_canvasGroup.alpha == 0f)
                {
                    // Do not animate a fade out if this is already invisible
                    gameObject.SetActive(false);
                    return;
                }
                
                _currentCoroutine = StartCoroutine(HideCoroutine());
            }
        }

        private IEnumerator ShowCoroutine()
        {
            // Fade in
            yield return _canvasGroup
                .DOFade(1f, FADE_ANIM_LENGTH)
                .WaitForCompletion();
        }

        private IEnumerator HideCoroutine()
        {
            // Fade out
            yield return _canvasGroup
                .DOFade(0f, FADE_ANIM_LENGTH)
                .WaitForCompletion();

            gameObject.SetActive(false);
            _currentCoroutine = null;
        }

        private void StopCurrentCoroutine()
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }
        }

        private string GetMeasuresLeft(double endTime)
        {
            double currentTime = GameManager.SongTime;
            if (currentTime < _nextMeasureTime)
            {
                // Measure count has not changed
                return _countdownText.text;
            }
            
            var syncTrack = GameManager.Chart.SyncTrack;

            int newMeasuresLeft = 0;

            double timeRef = currentTime;
            while (timeRef < endTime)
            {
                var currentTimeSig = syncTrack.TimeSignatures.GetPrevious(timeRef);
                if (currentTimeSig == null)
                {
                    YargLogger.LogFormatDebug("Cannot calculate WaitCountdown measures at time {0}. No time signatures available.", timeRef);
                    break;
                }

                var currentTempo = syncTrack.Tempos.GetPrevious(timeRef);

                timeRef += currentTimeSig.GetSecondsPerMeasure(currentTempo);
                
                if (++newMeasuresLeft == 1)
                {
                    _nextMeasureTime = timeRef;
                }
            }

            return newMeasuresLeft.ToString();
        }
    }
}
