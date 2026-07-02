using DG.Tweening;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Helpers.Extensions;

namespace YARG.Gameplay.HUD
{
    public class UnisonIcon : GameplayBehaviour
    {
        [SerializeField]
        private Image _icon;
        [SerializeField]
        private Image _fill;

        private const float PROGRESS_FILL_ALPHA = 0.3f;
        private const float PROGRESS_COMPLETE_ALPHA = 0.5f;

        private float _targetProgress;


        [SerializeField]
        private Color _completeColor;
        [SerializeField]
        private Color _failColor;

        private Sequence _completeSequence;

        private bool _isFailState;

        protected override void GameplayAwake()
        {
            _completeSequence = DOTween.Sequence()
                .Append(transform.DOScale(1.2f, 0.2f).SetEase(Ease.OutSine))
                .Append(transform.DOScale(1f, 0.2f).SetEase(Ease.OutSine))
                .Pause().SetLink(gameObject).SetAutoKill(false);
            _fill.fillAmount = 0f;
            _fill.color = _fill.color.WithAlpha(0.3f);
            _icon.color = Color.gray4;
        }

        public void SetIcon(string spritePath)
        {
            _icon.sprite = Addressables.LoadAssetAsync<Sprite>(spritePath).WaitForCompletion();
        }
        public void SetProgress(float progress)
        {
            if (_isFailState)
            {
                return;
            }

            _targetProgress = progress;
            if (progress >= 1f)
            {
                _icon.color = Color.white;
                _fill.color = _completeColor.WithAlpha(PROGRESS_COMPLETE_ALPHA);
                _completeSequence.Restart();
            }
            else
            {
                _icon.color = Color.gray4;
                _fill.color = Color.white.WithAlpha(PROGRESS_FILL_ALPHA);
            }
        }

        public void SetFailState(bool isFail)
        {
            _isFailState = isFail;
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

        private void Update()
        {
            if (_isFailState || Mathf.Approximately(_fill.fillAmount, _targetProgress))
            {
                return;
            }
            // Lerp to new progress
            _fill.fillAmount = DOVirtual.EasedValue(_fill.fillAmount, _targetProgress, Time.deltaTime * 5, Ease.OutSine);
        }
    }
}