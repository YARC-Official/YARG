using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Chart;

namespace YARG.Gameplay.HUD
{
    public class StarDisplay : MonoBehaviour
    {
        [SerializeField]
        private GameObject[] starObjects;

        [SerializeField]
        private GameObject[] goldMeterObjects;

        [SerializeField]
        private GameObject goldMeterParent;

        [SerializeField]
        private RawImage goldMeterLine;

        private GameManager _gameManager;

        private Animator _goldMeterParentAnimator;

        private IEnumerator<Beatline> _measures;

        private int _currentStar;

        private bool _isGoldAchieved;

        private float _goldMeterHeight;

        private void Awake()
        {
            _gameManager = FindObjectOfType<GameManager>();

            _goldMeterParentAnimator = goldMeterParent.GetComponent<Animator>();
            _goldMeterHeight = GetComponent<RectTransform>().rect.height;
        }

        private void Start()
        {
            _measures = _gameManager.Beats.Where(b => b.Type == BeatlineType.Measure).GetEnumerator();
            _measures.MoveNext();
        }

        private void Update()
        {
            if (_isGoldAchieved || _measures.Current is null)
            {
                return;
            }

            var lastMeasure = _measures.Current;
            while(_measures.Current?.Time < _gameManager.SongTime)
            {
                _measures.MoveNext();
            }

            if (_measures.Current is null || _measures.Current == lastMeasure)
            {
                return;
            }

            var time = _measures.Current.Time - lastMeasure.Time;

            _goldMeterParentAnimator.speed = 1f / (float)time;
            _goldMeterParentAnimator.Play("GoldMeter", -1, 0);
        }

        private void SetStarProgress(GameObject star, double progress)
        {
            if (!star.activeInHierarchy)
            {
                star.SetActive(true);
                star.GetComponent<Animator>().Play("PopNew");
            }

            if (progress < 1)
            {
                star.transform.GetChild(0).GetComponent<Image>().fillAmount = (float) progress;
            }
            else
            {
                // Fill the star
                star.transform.GetChild(0).GetComponent<Image>().fillAmount = 1;
                star.GetComponent<Animator>().Play("TransToComplete");
            }
        }

        public void SetStars(double stars)
        {
            if (_isGoldAchieved)
            {
                return;
            }

            int topStar = (int) stars;

            double starProgress = stars - topStar;

            if (_currentStar < 5)
            {
                if (topStar > _currentStar)
                {
                    for (int i = _currentStar; i < topStar; ++i)
                    {
                        SetStarProgress(starObjects[i], 1);
                    }

                    _currentStar = topStar;

                    GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarGain);
                    Debug.Log($"Gained star at {_gameManager.BandScore} ({stars})");
                }

                if (_currentStar < 5)
                {
                    SetStarProgress(starObjects[_currentStar], starProgress);
                }
            }

            if (stars is >= 5 and < 6.0)
            {
                foreach (var meter in goldMeterObjects)
                {
                    meter.GetComponent<Image>().fillAmount = (float) starProgress;
                }

                goldMeterLine.rectTransform.anchoredPosition = new Vector2(0, (float) (starProgress * _goldMeterHeight));
            } else if (stars >= 6)
            {
                foreach(var star in starObjects)
                {
                    star.GetComponent<Animator>().Play("TransToGold");
                }

                goldMeterParent.SetActive(false);

                GlobalVariables.AudioManager.PlaySoundEffect(SfxSample.StarGold);
                _isGoldAchieved = true;
            }
        }
    }
}