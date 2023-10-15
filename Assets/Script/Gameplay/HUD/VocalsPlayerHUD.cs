using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class VocalsPlayerHUD : MonoBehaviour
    {
        [SerializeField]
        private Image _comboMeterFill;
        [SerializeField]
        private Image _starPowerFill;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _multiplierText;
        [SerializeField]
        private TextMeshProUGUI _performanceText;

        private float _comboMeterFillTarget;

        private readonly PerformanceTextScaler _scaler = new(2f);
        private Coroutine _currentCoroutine;

        private void Awake()
        {
            _performanceText.text = string.Empty;
        }

        private void Update()
        {
            if (_comboMeterFillTarget == 0f)
            {
                // Go to zero instantly
                _comboMeterFill.fillAmount = 0f;
            }
            else
            {
                _comboMeterFill.fillAmount = Mathf.Lerp(_comboMeterFill.fillAmount,
                    _comboMeterFillTarget, Time.deltaTime * 12f);
            }
        }

        public void UpdateInfo(float phrasePercent, int multiplier, float starPowerPercent)
        {
            _comboMeterFillTarget = phrasePercent;

            _multiplierText.text = multiplier != 1 ? $"{multiplier}<sub>x</sub>" : string.Empty;

            _starPowerFill.fillAmount = starPowerPercent;
        }

        public void ShowPhraseHit(double hitPercent)
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
            }

            _currentCoroutine = StartCoroutine(ShowNextNotification(hitPercent));
        }

        private IEnumerator ShowNextNotification(double hitPercent)
        {
            _performanceText.text = hitPercent switch
            {
                >= 1f   => "AWESOME!",
                >= 0.8f => "STRONG",
                >= 0.7f => "GOOD",
                >= 0.6f => "OKAY",
                >= 0.1f => "MESSY",
                _       => "AWFUL"
            };

            _scaler.ResetAnimationTime();

            while (_scaler.AnimTimeRemaining > 0f)
            {
                _scaler.AnimTimeRemaining -= Time.deltaTime;
                float scale = _scaler.PerformanceTextScale();

                _performanceText.transform.localScale = new Vector3(scale, scale, scale);
                yield return null;
            }

            _performanceText.text = string.Empty;
            _currentCoroutine = null;
        }
    }
}