﻿using System.Collections;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Chart;
using YARG.Core.Game;

namespace YARG.Gameplay.HUD
{
    public class VocalsPlayerHUD : GameplayBehaviour
    {
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

        private readonly PerformanceTextScaler _scaler = new(2f);
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