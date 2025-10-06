using System;
using System.Collections;
using Cysharp.Text;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Localization;
using YARG.Player;

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
        private TextNotifications _textNotifications;

        [SerializeField]
        private PlayerNameDisplay _playerNameDisplay;

        private float _comboMeterFillTarget;

        private Coroutine _hudCoroutine;

        private bool _shouldPulse;
        private bool _hudShowing = true;

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
            if (_shouldPulse)
            {
                float pulse = 1 - (float) GameManager.BeatEventHandler.Visual.StrongBeat.CurrentPercentage;
                _starPowerPulse.color = Color.white.WithAlpha(pulse);
            }
            else
            {
                _starPowerPulse.color = Color.white.WithAlpha(0);
            }
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

        public static string GetVocalPerformanceText(double hitPercent)
        {
            string performanceKey = hitPercent switch
            {
                >= 1f => "Awesome",
                >= 0.8f => "Strong",
                >= 0.7f => "Good",
                >= 0.6f => "Okay",
                >= 0.1f => "Messy",
                _ => "Awful"
            };

            return Localize.Key("Gameplay.Vocals.Performance", performanceKey);
        }

        public void SetHUDShowing(bool show)
        {
            if (_hudShowing == show)
            {
                return;
            }

            _hudShowing = show;

            if (_hudCoroutine != null)
            {
                StopCoroutine(_hudCoroutine);
            }

            _hudCoroutine = StartCoroutine(ShowHUD(_hudShowing));
        }

        private IEnumerator ShowHUD(bool show)
        {
            if (show)
            {
                yield return transform
                    .DORotate(new Vector3(0f, 0f, 0f), 0.25f)
                    .WaitForCompletion();
            }
            else
            {
                yield return transform
                    .DORotate(new Vector3(90f, 0f, 0f), 0.25f)
                    .WaitForCompletion();
            }

            _hudCoroutine = null;
        }

        public void ShowPlayerName(YargPlayer player, int needleId)
        {
            _playerNameDisplay.ShowPlayer(player, needleId);
        }

        public void ShowPhraseHit(double hitPercent, int combo)
        {
            if (!Settings.SettingsManager.Settings.DisableTextNotifications.Value)
            {
                _textNotifications.UpdateNoteStreak(combo);
            }
            var resultText = GetVocalPerformanceText(hitPercent);
            _textNotifications.ShowVocalPhraseResult(resultText, combo);
        }

        public void ShowNotification(TextNotificationType notificationType)
        {
            _textNotifications.ShowNotification(notificationType);
        }
    }
}