using System;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class UnisonIcon : GameplayBehaviour
    {
        private const float PROGRESS_FILL_ALPHA     = 0.5f;
        private const float PROGRESS_COMPLETE_ALPHA = 0.7f;
        private const float TWEEN_LENGTH              = 0.15f;
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Image _fill;
        [SerializeField]
        private Color _completeColor;
        [SerializeField]
        private Color _failColor;
        [SerializeField]
        private Color _progressColor;
        [SerializeField]
        private Color    _incompleteColor;
        private Sequence _completeSequence;
        private Tweener _seekTween;
        private bool     _hasFailed;
        private float    _progress;

        protected override void GameplayAwake()
        {
            _completeSequence = UnisonDisplay.BuildCompleteSequence(gameObject);
            _seekTween = _fill.DOFillAmount(_progress, TWEEN_LENGTH)
                .SetEase(Ease.OutCubic)
                .OnComplete(OnTweenComplete)
                .Pause()
                .SetAutoKill(false)
                .SetLink(gameObject);
            _fill.fillAmount = 0f;
            _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            _icon.color = _incompleteColor;
        }

        private void OnTweenComplete()
        {
            if (_progress < 1f)
            {
                return;
            }
            _completeSequence.Restart();
            _icon.color = Color.white;
            _fill.color = _completeColor.WithAlpha(PROGRESS_COMPLETE_ALPHA);
        }

        public async void SetIcon(string spritePath)
        {
            try
            {
                _icon.sprite = await Addressables.LoadAssetAsync<Sprite>(spritePath);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatError("Failed to load unison icon sprite at path: {0}. Exception: {1}", spritePath, e);
            }
        }

        public void SetProgress(float progress)
        {
            if (_hasFailed || Mathf.Approximately(_progress, progress))
            {
                return;
            }

            _progress = progress;
            SetTween();
        }

        private void SetTween()
        {
            // Lerp to new progress
            _seekTween.ChangeEndValue(_progress, true);
            _seekTween.Play();
        }

        public void SetFailState(bool isFail)
        {
            _hasFailed = isFail;
            if (isFail)
            {
                _icon.color = _failColor;
                _fill.color = _failColor.WithAlpha(PROGRESS_FILL_ALPHA);
            }
            else
            {
                SetProgress(_fill.fillAmount);
            }
        }

        public void ResetState()
        {
            _progress = 0f;
            _hasFailed = false;
            _fill.fillAmount = 0f;
            _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            _icon.color = _incompleteColor;
            gameObject.SetActive(false);
        }
    }
}