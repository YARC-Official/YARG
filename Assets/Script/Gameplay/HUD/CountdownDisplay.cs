using System.Collections;
using TMPro;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;

namespace YARG
{
    public class CountdownDisplay : MonoBehaviour
    {
        [SerializeField]
        private Image _backgroundCircle;
        [SerializeField]
        private TextMeshProUGUI _countdownText;
        [SerializeField]
        private Image _getReady;

        [Space]
        [SerializeField]
        private CanvasGroup _canvasGroup;

        private Coroutine _currentCoroutine;

        private int _measuresLeft;

        public void UpdateCountdown(int measuresLeft)
        {
            if (measuresLeft == _measuresLeft)
            {
                return; 
            }

            _measuresLeft = measuresLeft;

            if (measuresLeft <= WaitCountdown.END_COUNTDOWN_MEASURE)
            {
                // New measure count is below the threshold where the countdown display should be hidden
                ToggleDisplay(false);
                return;
            }

            // New measure count is above display threshold
            if (measuresLeft > WaitCountdown.GET_READY_MEASURE)
            {
                _countdownText.text = measuresLeft.ToString();

                _getReady.gameObject.SetActive(false);
                _backgroundCircle.gameObject.SetActive(true);
            }
            else if (measuresLeft <= WaitCountdown.GET_READY_MEASURE)
            {
                // Change display from number to "Get Ready!"
                _backgroundCircle.gameObject.SetActive(false);
                _getReady.gameObject.SetActive(true);
            }

            ToggleDisplay(true);
        }

        public void ForceReset()
        {
            StopCurrentCoroutine();

            gameObject.SetActive(false);

             _currentCoroutine = null;
        }

        private void ToggleDisplay(bool newState)
        {
            if (newState == gameObject.activeSelf)
            {
                return;
            }

            StopCurrentCoroutine();

            if (newState)
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
                .DOFade(1f, 0.45f)
                .WaitForCompletion();
        }

        private IEnumerator HideCoroutine()
        {
            // Fade out
            yield return _canvasGroup
                .DOFade(0f, 0.45f)
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
