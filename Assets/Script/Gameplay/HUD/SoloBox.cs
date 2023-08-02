using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class SoloBox : MonoBehaviour
    {
        [SerializeField]
        private Image           _soloBox;
        [SerializeField]
        private TextMeshProUGUI _soloTopText;
        [SerializeField]
        private TextMeshProUGUI _soloBottomText;
        [SerializeField]
        private TextMeshProUGUI _soloFullText;
        [SerializeField]
        private CanvasGroup     _soloBoxCanvasGroup;

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

        private int _noteCount;
        private int _hitCount;
        private int _soloBonus;

        private int HitPercent => Mathf.FloorToInt((float) _hitCount / _noteCount * 100f);

        private bool _inSolo;

        private Coroutine _currentCoroutine = null;

        public void StartSolo(int noteCount)
        {
            // Don't even bother if the solo has no points
            if (noteCount == 0) return;

            _noteCount = noteCount;
            _inSolo = true;
            gameObject.SetActive(true);

            StopCurrentCoroutine();

            _currentCoroutine = StartCoroutine(ShowCoroutine(noteCount));
        }

        private IEnumerator ShowCoroutine(int noteCount)
        {
            _soloFullText.text = string.Empty;
            _soloBox.sprite = _soloSpriteNormal;

            // Set some dummy text
            _soloTopText.text = "0%";
            _soloBottomText.text = $"0/{noteCount}";

            // Fade in the box
            yield return _soloBoxCanvasGroup
                .DOFade(1f, 0.25f)
                .WaitForCompletion();
        }

        public void HitNote()
        {
            if (!_inSolo) return;

            _hitCount++;

            _soloTopText.text = $"{HitPercent}%";
            _soloBottomText.text = $"{_hitCount}/{_noteCount}";
        }

        public void EndSolo(int soloBonus)
        {
            StopCurrentCoroutine();

            _inSolo = false;
            _currentCoroutine = StartCoroutine(HideCoroutine(soloBonus));
        }

        private IEnumerator HideCoroutine(int soloBonus)
        {
            // Hide the top and bottom text
            _soloTopText.text = string.Empty;
            _soloBottomText.text = string.Empty;

            // Get the correct gradient and color
            var (sprite, gradient) = HitPercent switch
            {
                >= 100 => (_soloSpritePerfect, _soloGradientPerfect),
                >= 60  => (_soloSpriteNormal, _soloGradientNormal),
                _      => (_soloSpriteMessy, _soloGradientMessy),
            };
            _soloBox.sprite = sprite;
            _soloFullText.colorGradientPreset = gradient;

            // Display final hit percentage
            _soloFullText.text = $"{HitPercent}%";

            yield return new WaitForSeconds(1f);

            // Show performance text
            string resultText = HitPercent switch
            {
                > 100 => "HOW!?",
                  100 => "PERFECT\nSOLO!",
                >= 95 => "AWESOME\nSOLO!",
                >= 90 => "GREAT\nSOLO!",
                >= 80 => "GOOD\nSOLO!",
                >= 70 => "SOLID\nSOLO",
                   69 => "<i>NICE</i>\nSOLO",
                >= 60 => "OKAY\nSOLO",
                >= 0  => "MESSY\nSOLO",
                <  0  => "HOW!?",
            };
            _soloFullText.text = resultText;

            yield return new WaitForSeconds(1f);

            // Show point bonus
            _soloFullText.text = $"{soloBonus:N0}\nPOINTS";

            yield return new WaitForSeconds(1f);

            // Fade out the box
            yield return _soloBoxCanvasGroup
                .DOFade(0f, 0.25f)
                .WaitForCompletion();

            _soloBox.gameObject.SetActive(false);
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