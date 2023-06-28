using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.PlayMode;
using YARG.Settings;

namespace YARG.UI
{
    public class TrackView : MonoBehaviour
    {
        [field: SerializeField]
        public RawImage TrackImage { get; private set; }

        [SerializeField]
        private AspectRatioFitter _aspectRatioFitter;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _performanceText;

        [SerializeField]
        private PerformanceTextScaler _performanceTextScaler;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _soloTopText;

        [SerializeField]
        private TextMeshProUGUI _soloBottomText;

        [SerializeField]
        private TextMeshProUGUI _soloFullText;

        [SerializeField]
        private CanvasGroup _soloBoxCanvasGroup;

        [SerializeField]
        private Image _soloBox;

        [Space]
        [SerializeField]
        private Sprite _soloSpriteNormal;

        [SerializeField]
        private Sprite _soloSpritePerfect;

        [SerializeField]
        private Sprite _soloSpriteMessy;

        [SerializeField]
        private TMP_ColorGradient _soloGradientNormal;

        [SerializeField]
        private TMP_ColorGradient _soloGradientPerfect;

        [SerializeField]
        private TMP_ColorGradient _soloGradientMessy;

        private Coroutine _soloBoxHide = null;

        private void Start()
        {
            _performanceTextScaler = new(3f);
            _performanceText.text = "";
            _aspectRatioFitter.aspectRatio = (float) Screen.width / Screen.height;
        }

        public void UpdateSizing(int trackCount)
        {
            float scale = Mathf.Max(0.7f * Mathf.Log10(trackCount - 1), 0f);
            scale = 1f - scale;

            TrackImage.transform.localScale = new Vector3(scale, scale, scale);
        }

        public void SetSoloBox(int hitPercent, int notesHit, int totalNotes)
        {
            // Stop hide coroutine if we were previously hiding
            if (_soloBoxHide != null)
            {
                StopCoroutine(_soloBoxHide);
                _soloBoxHide = null;
            }

            string percentageText = $"{hitPercent}%";
            string noteCountText = $"{notesHit}/{totalNotes}";

            // Show solo box
            _soloBox.gameObject.SetActive(true);
            _soloBox.sprite = _soloSpriteNormal;
            _soloBoxCanvasGroup.alpha = 1f;

            // Set solo text
            _soloFullText.text = string.Empty;
            _soloTopText.text = percentageText;
            _soloBottomText.text = noteCountText;
        }

        public void HideSoloBox(int finalPercent, double scoreBonus)
        {
            _soloTopText.text = string.Empty;
            _soloBottomText.text = string.Empty;

            _soloBoxHide = StartCoroutine(HideSoloBoxCoroutine(finalPercent, scoreBonus));
        }

        private IEnumerator HideSoloBoxCoroutine(int finalPercent, double scoreBonus)
        {
            // Set textbox color
            var (sprite, gradient) = finalPercent switch
            {
                >= 100 => (_soloSpritePerfect, _soloGradientPerfect),
                >= 60  => (_soloSpriteNormal, _soloGradientNormal),
                _      => (_soloSpriteMessy, _soloGradientMessy),
            };
            _soloBox.sprite = sprite;
            _soloFullText.colorGradientPreset = gradient;

            // Display final hit percentage
            _soloFullText.text = $"{finalPercent}%";

            yield return new WaitForSeconds(1f);

            // Show performance text
            string resultText = finalPercent switch
            {
                > 100 => "HOW!?",
                100   => "PERFECT\nSOLO!",
                >= 95 => "AWESOME\nSOLO!",
                >= 90 => "GREAT\nSOLO!",
                >= 80 => "GOOD\nSOLO!",
                >= 70 => "SOLID\nSOLO",
                69    => "<i>NICE</i>\nSOLO",
                >= 60 => "OKAY\nSOLO",
                >= 0  => "MESSY\nSOLO",
                < 0   => "HOW!?",
            };
            _soloFullText.text = resultText;

            yield return new WaitForSeconds(1f);

            // Show point bonus
            _soloFullText.text = $"{Math.Round(scoreBonus)}\nPOINTS";

            yield return new WaitForSeconds(1f);

            // Fade out the box
            yield return _soloBoxCanvasGroup
                .DOFade(0f, 0.25f)
                .WaitForCompletion();

            _soloBox.gameObject.SetActive(false);
            _soloBoxHide = null;
        }

        public void ShowPerformanceText(string text)
        {
            if (SettingsManager.Settings.DisableTextNotifications.Data)
            {
                return;
            }

            StopCoroutine(nameof(ScalePerformanceText));
            StartCoroutine(ScalePerformanceText(text));
        }

        private IEnumerator ScalePerformanceText(string text)
        {
            var rect = _performanceText.rectTransform;
            rect.localScale = Vector3.zero;

            _performanceText.text = text;
            _performanceTextScaler.ResetAnimationTime();

            while (_performanceTextScaler.AnimTimeRemaining > 0f)
            {
                _performanceTextScaler.AnimTimeRemaining -= Time.deltaTime;
                var scale = _performanceTextScaler.PerformanceTextScale();
                rect.localScale = new Vector3(scale, scale, scale);

                // Update animation every frame
                yield return null;
            }

            _performanceText.text = string.Empty;
        }
    }
}