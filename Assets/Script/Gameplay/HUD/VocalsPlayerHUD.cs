using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Helpers.UI;
using YARG.Localization;
using YARG.Playback;
using YARG.Player;
using YARG.Settings;

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
        private GameObject _multiplierTextContainer; // Required as the different multipliers are not the same object.
        [SerializeField]
        private TextNotifications _textNotifications;
        [SerializeField]
        private PlayerNameDisplay _playerNameDisplay;
        [SerializeField]
        private Image _multiplierRim;
        [SerializeField]
        private VocalSunburstEffects _sunburstEffects;
        [SerializeField]
        private Image _fcRing;
        [Header("Combo Rim Images")]
        [SerializeField]
        private Image _grooveRim;
        [SerializeField]
        private Image _starPowerRim;

        private Sequence _multiplierIncreaseSequence;
        private bool     _isSp;
        private bool     _isFc = true;
        private int      _multiplier = 1;

        private float _comboMeterFillTarget;

        private Coroutine _hudCoroutine;

        private bool                             _shouldPulse;
        private bool                             _hudShowing = true;
        private TextMeshProUGUI[] _textCache;

        public void Initialize(EnginePreset enginePreset)
        {
            GameManager.BeatEventHandler.Visual.Subscribe(_sunburstEffects.PulseSunburst, BeatEventType.StrongBeat);

            _multiplierIncreaseSequence = DOTween.Sequence(_multiplierTextContainer)
                .Append(_multiplierTextContainer.transform.DOScale(1.75f, 0.15f))
                .Join(_multiplierTextContainer.transform.DOLocalMoveX(-30f, 0.15f))
                .Append(_multiplierTextContainer.transform.DOScale(1f, 0.15f))
                .Join(_multiplierTextContainer.transform.DOLocalMoveX(0f, 0.15f))
                .SetAutoKill(false);
            _sunburstEffects.SetSunburstEffects(false, false, 1);
            _textCache = MultiplierTextHelper.CreateMultiplierTextCache(EnginePreset.DEFAULT_MAX_MULTIPLIER, _multiplierText, GameManager.Players.Count > 1);

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

            _starPowerFill.fillAmount = 0f;
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

            if (!_isFc)
            {
                var spRimAlpha = Mathf.Clamp01(_starPowerRim.color.a + (_isSp ? 1 : -1) * 3f * Time.deltaTime);
                var grooveRimAlpha = Mathf.Clamp01(_grooveRim.color.a + (!_isSp && _multiplier == 4 ? 1 : -1) * 3f * Time.deltaTime);

                _grooveRim.color = Color.white.WithAlpha(grooveRimAlpha);
                _starPowerRim.color = Color.white.WithAlpha(spRimAlpha);
            }
        }

        public void UpdateInfo(float phrasePercent, int multiplier,
            float starPowerPercent, bool isStarPowerActive)
        {
            _comboMeterFillTarget = phrasePercent;

            _starPowerFill.fillAmount = starPowerPercent;
            _starPowerPulse.fillAmount = starPowerPercent;

            _shouldPulse = isStarPowerActive || starPowerPercent >= 0.5;


            _sunburstEffects.SetSunburstEffects(multiplier == 4 && !isStarPowerActive, isStarPowerActive, multiplier);

            if (multiplier == _multiplier && isStarPowerActive == _isSp)
            {
                return;
            }
            _multiplierText.enabled = false;

            if (multiplier > 1)
            {
                _multiplierText = _textCache[multiplier - 2];
                _multiplierText.enabled = true;
                if (isStarPowerActive == _isSp && multiplier > _multiplier)
                {
                    _multiplierIncreaseSequence.Restart();
                }
            }
            _multiplier = multiplier;
            _isSp = isStarPowerActive;
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
            if (!SettingsManager.Settings.DisableTextNotifications.Value)
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

        public void SetFullCombo(bool isFullCombo)
        {
            _isFc = isFullCombo;
            if (isFullCombo)
            {
                _fcRing.gameObject.SetActive(true);
            }
            else
            {
                // Instantly show the SP rim if in star power
                if (_isSp)
                {
                    _starPowerRim.color = Color.white.WithAlpha(1f);
                }
                _fcRing.gameObject.SetActive(false);
            }
        }
    }
}