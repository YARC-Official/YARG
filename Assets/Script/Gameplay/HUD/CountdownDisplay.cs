using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Settings;
using System;

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

        public void UpdateCountdown(int measuresLeft, double countdownLength, double endTime)
        {
            if (DisplayStyle == CountdownDisplayMode.Disabled)
            {
                return;
            }

            ToggleDisplay(measuresLeft > WaitCountdown.END_COUNTDOWN_MEASURE);
            
            if (!gameObject.activeSelf)
            {
                return;
            }
            
            double currentTime = GameManager.SongTime;

            int displayNumber = DisplayStyle switch
            {
                CountdownDisplayMode.Measures => measuresLeft,
                CountdownDisplayMode.Seconds => (int) Math.Ceiling(endTime - currentTime),
                _ => throw new Exception("Unreachable")
            };

            _countdownText.text = displayNumber.ToString();
            
            _progressBar.fillAmount = (float) ((endTime - currentTime) / countdownLength);
        }

        public void ForceReset()
        {
            StopCurrentCoroutine();

            gameObject.SetActive(false);

             _currentCoroutine = null;
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
                _currentCoroutine = StartCoroutine(HideCoroutine());
            }
        }

        private IEnumerator ShowCoroutine()
        {
            // Fade in
            yield return _canvasGroup
                .DOFade(1f, WaitCountdown.FADE_ANIM_LENGTH)
                .WaitForCompletion();
        }

        private IEnumerator HideCoroutine()
        {
            // Fade out
            yield return _canvasGroup
                .DOFade(0f, WaitCountdown.FADE_ANIM_LENGTH)
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
