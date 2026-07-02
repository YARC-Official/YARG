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

        private Sequence _completeSequence;

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
            if (progress >= 0)
            {
                _fill.fillAmount = progress;
            }

            if (progress >= 1f)
            {
                _icon.color = Color.white;
                _fill.color = Color.gold.WithAlpha(0.5f);
                _completeSequence.Restart();
            }
            else if (progress < 0)
            {
                _icon.color = Color.red;
                _fill.color = Color.red.WithAlpha(0.3f);
            }
            else
            {
                _icon.color = Color.gray4;
                _fill.color = Color.white.WithAlpha(0.3f);
            }
        }
    }
}