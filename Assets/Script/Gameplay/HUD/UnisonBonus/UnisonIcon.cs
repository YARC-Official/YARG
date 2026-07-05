using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class UnisonIcon : GameplayBehaviour
    {
        private const float PROGRESS_FILL_ALPHA     = 0.3f;
        private const float PROGRESS_COMPLETE_ALPHA = 0.5f;
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
        private bool     _hasFailed;
        private float    _targetProgress;

        private void Update()
        {
            if (Mathf.Approximately(_fill.fillAmount, _targetProgress))
            {
                return;
            }

            // Lerp to new progress
            _fill.fillAmount =
                DOVirtual.EasedValue(_fill.fillAmount, _targetProgress, Time.deltaTime * 15, Ease.OutSine);

            if (_hasFailed)
            {
                return;
            }

            if (_fill.fillAmount >= 0.99f)
            {
                _icon.color = Color.white;
                _fill.color = _completeColor.WithAlpha(PROGRESS_COMPLETE_ALPHA);
            }
            else
            {
                _icon.color = _incompleteColor;
                _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            }
        }

        protected override void GameplayAwake()
        {
            _completeSequence = UnisonDisplay.BuildCompleteSequence(gameObject);
            _fill.fillAmount = 0f;
            _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            _icon.color = _incompleteColor;
        }

        public void SetIcon(string spritePath)
        {
            _icon.sprite = Addressables.LoadAssetAsync<Sprite>(spritePath).WaitForCompletion();
        }

        public void SetProgress(float progress)
        {
            if (_hasFailed)
            {
                return;
            }

            _targetProgress = progress;

            if (progress >= 1f)
            {
                _completeSequence.Restart();
            }
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
            _targetProgress = 0f;
            _hasFailed = false;
            _fill.fillAmount = 0f;
            _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            _icon.color = _incompleteColor;
            gameObject.SetActive(false);
        }
    }
}