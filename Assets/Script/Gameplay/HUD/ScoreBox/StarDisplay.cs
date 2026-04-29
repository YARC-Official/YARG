using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class StarDisplay : MonoBehaviour
    {
        private enum State
        {
            Hidden,
            Progress,
            Completed,
            CompletedGold
        }

        [SerializeField]
        private Image _starProgress;
        [SerializeField]
        private Image _completedStar;
        [SerializeField]
        private Image _completedGold;
        [SerializeField]
        private Image _white;

        [Space]
        [SerializeField]
        private CanvasGroup _goldProgressGroup;
        [SerializeField]
        private Image _goldProgress;
        [SerializeField]
        private RawImage _goldProgressLine;

        private float _goldMeterHeight;

        private Sequence _popNewSequence;
        private Sequence _completedStarSequence;
        private Sequence _completedGoldSequence;
        private State    _state = State.Hidden;

        private void Awake()
        {
            _goldMeterHeight = _goldProgress.rectTransform.rect.height;
            var t = gameObject.transform;
            _popNewSequence = DOTween.Sequence().Append(t.DOScale(1.4f, 0.3f)).Append(t.DOScale(1f, 0.2f))
                .SetAutoKill(false).Pause().SetLink(gameObject);
            _completedStarSequence = DOTween.Sequence().Append(_completedStar.DOFillAmount(1f, 0.33f))
                .Join(t.DOScale(1.5f, 0.166f)).Append(t.DOScale(1f, 0.166f)).SetAutoKill(false).Pause()
                .SetLink(gameObject);
            _completedGoldSequence = DOTween.Sequence().Append(t.DOScale(1.6f, 0.25f))
                .Insert(0.04f, _white.DOFade(1f, 0.21f)).Append(t.DOScale(1f, 0.25f))
                .Insert(0.25f, _white.DOFade(0f, 0.25f)).Insert(0.25f, _completedGold.DOFade(1f, 0.25f))
                .SetAutoKill(false).Pause().SetLink(gameObject);
        }

        public void PopNew()
        {
            if (_state != State.Hidden)
            {
                return;
            }

            GetComponent<Image>().fillAmount = 1;
            _state = State.Progress;
            _popNewSequence.Restart();
        }

        public void HideStar()
        {
            _state = State.Hidden;

            _popNewSequence.Rewind();
            _completedStarSequence.Rewind();
            _completedGoldSequence.Rewind();

            _starProgress.fillAmount = 0;
            _starProgress.enabled = true;
            _completedStar.fillAmount = 0;

            _goldProgressGroup.gameObject.SetActive(true);
            _goldProgress.fillAmount = 0;
            _goldProgressLine.rectTransform.anchoredPosition = Vector2.zero;

            gameObject.transform.localScale = Vector3.zero;
        }

        public void SetGoldPulse(float pulse)
        {
            _goldProgressGroup.alpha = pulse;
        }

        public void SetProgress(float progress)
        {
            if (progress < 1)
            {
                if (_state is State.Completed or State.CompletedGold)
                {
                    _completedStarSequence.Rewind();
                    _state = State.Progress;
                }

                _completedStarSequence.Pause();
                _completedStar.fillAmount = 0;
                _starProgress.enabled = true;
                _starProgress.fillAmount = progress;
            }
            else if (_state == State.Progress)
            {
                // Finish the star
                _starProgress.fillAmount = 1;
                _starProgress.enabled = false;
                _state = State.Completed;
                _completedStarSequence.Restart();
            }
        }

        public void SetGoldProgress(float progress)
        {
            if (progress <= 0)
            {
                if (_state == State.CompletedGold)
                {
                    _state = State.Completed;
                }

                _completedGoldSequence.Rewind();
                _goldProgressGroup.gameObject.SetActive(true);
                _goldProgress.fillAmount = 0;
                _goldProgressLine.rectTransform.anchoredPosition = Vector2.zero;
            }
            else if (progress < 1)
            {
                if (_state == State.CompletedGold)
                {
                    _state = State.Completed;
                    _completedGoldSequence.Rewind();
                    _goldProgressGroup.gameObject.SetActive(true);
                }

                _goldProgress.fillAmount = progress;
                _goldProgressLine.rectTransform.anchoredPosition = new Vector2(0, progress * _goldMeterHeight);
            }
            else
            {
                // Finish the gold star
                _goldProgress.fillAmount = 1;
                _goldProgressGroup.gameObject.SetActive(false);
                if (_state == State.Completed)
                {
                    _state = State.CompletedGold;
                    _completedGoldSequence.Restart();
                }
            }
        }
    }
}