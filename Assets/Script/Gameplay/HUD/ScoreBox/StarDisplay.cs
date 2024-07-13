using System;
using UnityEngine;
using UnityEngine.UI;

namespace YARG.Gameplay.HUD
{
    public class StarDisplay : MonoBehaviour
    {
        public enum Animation
        {
            PopNew,
            Completed,
            Gold,
        }

        private const string ANIMATION_POP_NEW   = "PopNew";
        private const string ANIMATION_COMPLETED = "Completed";
        private const string ANIMATION_GOLD      = "Gold";

        private const string ANIMATION_GOLD_METER = "GoldMeter";

        [SerializeField]
        private Image _starProgress;
        [SerializeField]
        private Animator _starAnimator;

        [Space]
        [SerializeField]
        private GameObject _goldProgressContainer;
        [SerializeField]
        private Image _goldProgress;
        [SerializeField]
        private RawImage _goldProgressLine;
        [SerializeField]
        private Animator _goldMeterAnimator;

        private float _goldMeterHeight;

        private void Awake()
        {
            _goldMeterHeight = _goldProgress.rectTransform.rect.height;
        }

        public void PopNew()
        {
            gameObject.SetActive(true);
            _starAnimator.Play(ANIMATION_POP_NEW);
        }

        public void PulseGoldMeter()
        {
            // TODO: Use animation triggers instead
            // These arguments are required for it to properly loop
            _goldMeterAnimator.Play(ANIMATION_GOLD_METER, -1, 0f);
        }

        public void SetProgress(float progress)
        {
            if (progress < 1)
            {
                // Fill the star progress
                _starProgress.fillAmount = (float) progress;
            }
            else
            {
                // Finish the star
                _starProgress.fillAmount = 1;
                _starAnimator.Play(ANIMATION_COMPLETED);
            }
        }

        public void SetGoldProgress(float progress)
        {
            if (progress < 1)
            {
                // Fill the gold progress
                _goldProgress.fillAmount = (float) progress;
                _goldProgressLine.rectTransform.anchoredPosition = new Vector2(0, progress * _goldMeterHeight);
            }
            else
            {
                // Finish the gold star
                _goldProgress.fillAmount = 1;
                _goldProgressContainer.gameObject.SetActive(false);

                _starAnimator.Play(ANIMATION_GOLD);
            }
        }
    }
}