using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using System;
using Cysharp.Text;

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

            switch (DisplayStyle)
            {
                case CountdownDisplayMode.Seconds:
                {
                    _countdownText.SetText((int) Math.Ceiling(timeRemaining));
                    break;
                }
                case CountdownDisplayMode.Measures:
                {
                    var syncTrack = GameManager.Chart.SyncTrack;
                    uint measureTick = syncTrack.TimeToMeasureTick(currentTime);
                    uint endMeasureTick = syncTrack.TimeToMeasureTick(endTime);
                    uint remainingMeasures = (endMeasureTick - measureTick) / syncTrack.MeasureResolution;
                    _countdownText.SetText(remainingMeasures);
                    break;
                }
            }

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
    }
}
