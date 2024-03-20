using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;

namespace YARG.Gameplay.HUD
{
    public class StarScoreDisplay : GameplayBehaviour
    {
        private const string ANIMATION_POP_NEW   = "PopNew";
        private const string ANIMATION_COMPLETED = "Completed";
        private const string ANIMATION_GOLD      = "Gold";

        private const string ANIMATION_GOLD_METER = "GoldMeter";

        [SerializeField]
        private GameObject[] _starObjects;

        [SerializeField]
        private GameObject _goldMeterParent;
        [SerializeField]
        private GameObject[] _goldMeterObjects;
        [SerializeField]
        private RawImage _goldMeterLine;

        private Animator _goldMeterParentAnimator;

        private int _currentStar;
        private bool _isGoldAchieved;

        private float _goldMeterHeight;
        private int _goldMeterBeatCount;

        protected override void OnChartLoaded(SongChart chart)
        {
            _goldMeterParentAnimator = _goldMeterParent.GetComponent<Animator>();
            _goldMeterHeight = GetComponent<RectTransform>().rect.height;

            GameManager.BeatEventHandler.Subscribe(PulseGoldMeter);
        }

        protected override void GameplayDestroy()
        {
            GameManager.BeatEventHandler.Unsubscribe(PulseGoldMeter);
        }

        private void PulseGoldMeter(Beatline beat)
        {
            if (beat.Type == BeatlineType.Weak)
                return;

            _goldMeterBeatCount++;
            if (_goldMeterBeatCount % 2 == 0 && _goldMeterParent.activeInHierarchy)
            {
                // TODO: Use animation triggers instead
                // These arguments are required for it to properly loop
                _goldMeterParentAnimator.Play(ANIMATION_GOLD_METER, -1, 0f);
            }
        }

        public void SetStars(float stars)
        {
            if (_isGoldAchieved)
            {
                // We don't need to update anymore, because you can't get any higher!
                return;
            }

            int topStar = (int) stars;
            float starProgress = stars - topStar;

            if (_currentStar < 5)
            {
                if (topStar > _currentStar)
                {
                    for (int i = _currentStar; i < topStar && i < _starObjects.Length; i++)
                    {
                        SetStarProgress(_starObjects[i], 1);
                    }

                    _currentStar = topStar;

                    GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGain);
                    YargLogger.LogFormatDebug("Gained star at {0} ({1})", GameManager.BandScore, stars);
                }

                if (_currentStar < 5)
                {
                    SetStarProgress(_starObjects[_currentStar], starProgress);
                }
            }

            if (stars is >= 5f and < 6f)
            {
                foreach (var meter in _goldMeterObjects)
                {
                    meter.GetComponent<Image>().fillAmount = starProgress;
                }

                _goldMeterLine.rectTransform.anchoredPosition = new Vector2(0, starProgress * _goldMeterHeight);
            }
            else if (stars >= 6f)
            {
                foreach (var star in _starObjects)
                {
                    star.GetComponent<Animator>().Play(ANIMATION_GOLD);
                }

                _goldMeterParent.SetActive(false);

                GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGold);
                _isGoldAchieved = true;
            }
        }

        private static void SetStarProgress(GameObject star, double progress)
        {
            if (!star.activeSelf)
            {
                star.SetActive(true);
                star.GetComponent<Animator>().Play(ANIMATION_POP_NEW);
            }

            if (progress < 1)
            {
                // Fill the star progress
                var image = star.transform
                    .GetChild(0)
                    .GetComponent<Image>();
                image.fillAmount = (float) progress;
            }
            else
            {
                // Finish the star
                star.transform.GetChild(0).GetComponent<Image>().fillAmount = 1;
                star.GetComponent<Animator>().Play(ANIMATION_COMPLETED);
            }
        }
    }
}