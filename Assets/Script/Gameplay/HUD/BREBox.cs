using System;
using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Engine;
using YARG.Localization;

namespace YARG.Gameplay.HUD
{
    public class BREBox : MonoBehaviour
    {
        [SerializeField]
        private Image           _breBox;
        [SerializeField]
        private TextMeshProUGUI _breTopText;
        [SerializeField]
        private TextMeshProUGUI _breBottomText;
        [SerializeField]
        private TextMeshProUGUI _breFullText;
        [SerializeField]
        private CanvasGroup     _breBoxCanvasGroup;

        [Space]
        [SerializeField]
        private Sprite _breSpriteNormal;
        [SerializeField]
        private Sprite _breSpriteSuccess;
        [SerializeField]
        private Sprite _breSpriteFail;

        [SerializeField]
        private TMP_ColorGradient _breGradientNormal;
        [SerializeField]
        private TMP_ColorGradient _breGradientSuccess;
        [SerializeField]
        private TMP_ColorGradient _breGradientFail;

        private EngineManager _manager;

        private bool _showingForPreview;

        private Coroutine _currentCoroutine;

        private Vector3 _originalPosition;
        private Vector3 _originalScale;
        private float   _originalAlpha;

        private bool _codaEnding;

        public void Awake()
        {
            _originalPosition = _breBoxCanvasGroup.transform.localPosition;
            _originalScale = _breBoxCanvasGroup.transform.localScale;
            _originalAlpha = _breBoxCanvasGroup.alpha;
            gameObject.SetActive(false);
        }

        public void StartCoda(EngineManager manager)
        {
            if (gameObject.activeSelf)
            {
                return;
            }

            _manager = manager;

            gameObject.SetActive(true);

            StopCurrentCoroutine();

            _currentCoroutine = StartCoroutine(ShowCoroutine());
        }

        private IEnumerator ShowCoroutine()
        {
            _breFullText.text = string.Empty;
            _breBox.sprite = _breSpriteNormal;

            // Set some dummy text
            _breTopText.text = string.Empty;
            _breBottomText.text = string.Empty;

            _breFullText.text = Localize.KeyFormat("Gameplay.Solo.PointsResult", _manager.TotalCodaBonus);

            // Fade in the box
            yield return _breBoxCanvasGroup
                .DOFade(1f, 0.25f)
                .WaitForCompletion();
        }

        private void Update()
        {
            if (_showingForPreview || _codaEnding) return;

            _breFullText.text = Localize.KeyFormat("Gameplay.Solo.PointsResult", _manager.TotalCodaBonus);
        }

        public void EndCoda(int breBonus, Action endCallback)
        {
            if (!gameObject.activeSelf || _codaEnding)
            {
                return;
            }

            _codaEnding = true;
            StopCurrentCoroutine();

            _currentCoroutine = StartCoroutine(HideCoroutine(breBonus, endCallback));
        }

        public void ForceReset()
        {
            StopCurrentCoroutine();

            _breBox.gameObject.SetActive(false);

            _breBoxCanvasGroup.transform.localPosition = _originalPosition;
            _breBoxCanvasGroup.transform.localScale = _originalScale;
            _breBoxCanvasGroup.alpha = _originalAlpha;

            _breFullText.text = string.Empty;
            _breBox.sprite = _breSpriteNormal;
            _breFullText.colorGradientPreset = _breGradientNormal;

            _currentCoroutine = null;
            _manager = null;
            _codaEnding = false;
        }

        private IEnumerator HideCoroutine(int breBonus, Action endCallback)
        {
            // Hide the top and bottom text
            _breTopText.text = string.Empty;
            _breBottomText.text = string.Empty;

            var (sprite, gradient) = _manager.CodaSuccess switch
            {
                true  => (_breSpriteSuccess, _breGradientSuccess),
                false => (_breSpriteFail, _breGradientFail),
            };

            _breBox.sprite = sprite;
            _breFullText.colorGradientPreset = gradient;

            _breFullText.text = Localize.KeyFormat("Gameplay.Solo.PointsResult", breBonus);

            // Move the box so we aren't obscuring strong finish/full combo text
            _breBoxCanvasGroup.transform.DOMoveY(Screen.height / 2, 0.25f);

            // Go away sadly if BRE failed or triumphantly engorge if successful
            if (!_manager.CodaSuccess)
            {
                _breBoxCanvasGroup.transform.DOScale(0.01f, 0.25f);
            }
            else
            {
                _breBoxCanvasGroup.transform.DOScale(1.5f, 0.25f);
                yield return new WaitForSeconds(2f);
            }

            // Fade out the box
            yield return _breBoxCanvasGroup.DOFade(0f, 0.25f).WaitForCompletion();

            _breBox.gameObject.SetActive(false);
            _currentCoroutine = null;
            _manager = null;
            _codaEnding = false;

            endCallback?.Invoke();

            // Reset in case there's another BRE
            ForceReset();
        }

        private void StopCurrentCoroutine()
        {
            if (_currentCoroutine != null)
            {
                StopCoroutine(_currentCoroutine);
                _currentCoroutine = null;
            }
        }

        public void PreviewForEditMode(bool on)
        {
            if (on && !_breBox.gameObject.activeSelf)
            {
                _breBox.gameObject.SetActive(true);

                // Set preview solo box properties
                _breFullText.text = string.Empty;
                _breBox.sprite = _breSpriteNormal;
                _breFullText.text = Localize.KeyFormat("Gameplay.Solo.PointsResult", 6969);
                _breBoxCanvasGroup.alpha = 1f;

                _showingForPreview = true;
            }
            else if (!on && _showingForPreview)
            {
                _breBox.gameObject.SetActive(false);
                _showingForPreview = false;
            }
        }
    }
}