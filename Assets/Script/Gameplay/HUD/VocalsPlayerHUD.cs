using System.Collections;
using Cysharp.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Core.Game;

namespace YARG.Gameplay.HUD
{
    public class VocalsPlayerHUD : GameplayBehaviour
    {
        private const float ANIM_LENGTH = 1f;
        private const float ANIM_BASE_TO_PEAK_INTERVAL = 0.167f;
        private const float ANIM_PEAK_TO_VALLEY_INTERVAL = 0.167f;
        private const float ANIM_PEAK_SCALE = 1.1f;
        private const float ANIM_VALLEY_SCALE = 1f;

        [SerializeField]
        private Image _comboMeterFill;
        [SerializeField]
        private Image _starPowerFill;
        [SerializeField]
        private Image _starPowerPulse;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _multiplierText;
        [SerializeField]
        private TextMeshProUGUI _performanceText;

        private float _comboMeterFillTarget;

        private Coroutine _currentCoroutine;

        private bool _shouldPulse;

        protected override void OnChartLoaded(SongChart chart)
        {
            _performanceText.text = string.Empty;

            GameManager.BeatEventHandler.Subscribe(PulseBar);
        }

        protected override void GameplayDestroy()
        {
            GameManager.BeatEventHandler.Unsubscribe(PulseBar);
        }

        public void Initialize(EnginePreset enginePreset)
        {
            if (enginePreset == EnginePreset.Default)
            {
                // Don't change combo meter fill color if it's the default
            }
            else if (enginePreset == EnginePreset.Casual)
            {
                _comboMeterFill.color = new Color(0.9f, 0.3f, 0.9f);
            }
            else if (enginePreset == EnginePreset.Precision)
            {
                _comboMeterFill.color = new Color(1.0f, 0.9f, 0.0f);
            }
            else
            {
                // Otherwise, it must be a custom preset
                _comboMeterFill.color = new Color(1.0f, 0.25f, 0.25f);
            }
        }

        private void Update()
        {
            // Update combo meter
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

            // Update pulse
            if (_starPowerPulse.color.a > 0f)
            {
                var c = _starPowerPulse.color;
                c.a -= Time.deltaTime * 6f;
                _starPowerPulse.color = c;
            }
        }

        private void PulseBar(Beatline beat)
        {
            if (!_shouldPulse || beat.Type == BeatlineType.Weak)
                return;

            _starPowerPulse.color = Color.white;
        }

        public void UpdateInfo(float phrasePercent, int multiplier,
            float starPowerPercent, bool isStarPowerActive)
        {
            _comboMeterFillTarget = phrasePercent;

            if (multiplier != 1)
            {
                _multiplierText.SetTextFormat("{0}<sub>x</sub>", multiplier);
            }
            else
            {
                _multiplierText.text = string.Empty;
            }

            _starPowerFill.fillAmount = starPowerPercent;
            _starPowerPulse.fillAmount = starPowerPercent;

            _shouldPulse = isStarPowerActive || starPowerPercent >= 0.5;
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

            _performanceText.transform.localScale = Vector3.zero;

            const float animHoldInterval = ANIM_LENGTH
                - 2f * (ANIM_BASE_TO_PEAK_INTERVAL + ANIM_PEAK_TO_VALLEY_INTERVAL);

            yield return DOTween.Sequence()
                .Append(DOTween.Sequence()
                    .Append(_performanceText.transform
                        .DOScale(ANIM_PEAK_SCALE, ANIM_BASE_TO_PEAK_INTERVAL)
                        .SetEase(Ease.OutCirc))
                    .Append(_performanceText.transform
                        .DOScale(ANIM_VALLEY_SCALE, ANIM_PEAK_TO_VALLEY_INTERVAL)
                        .SetEase(Ease.InOutSine))
                    .AppendInterval(animHoldInterval))
                .Append(DOTween.Sequence()
                    .Append(_performanceText.transform
                        .DOScale(ANIM_PEAK_SCALE, ANIM_PEAK_TO_VALLEY_INTERVAL)
                        .SetEase(Ease.InOutSine))
                    .Append(_performanceText.transform
                        .DOScale(0f, ANIM_BASE_TO_PEAK_INTERVAL)
                        .SetEase(Ease.InCirc)))
                .WaitForCompletion();

            _performanceText.text = string.Empty;
            _currentCoroutine = null;
        }
    }
}