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
        [SerializeField]
        private StarDisplay[] _starObjects;

        private int _currentStar;
        private bool _isGoldAchieved;

        private float _goldMeterHeight;
        private int _goldMeterBeatCount;

        protected override void OnChartLoaded(SongChart chart)
        {
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
            if (_goldMeterBeatCount % 2 == 0 && _currentStar == 5 && !_isGoldAchieved)
            {
                foreach (var star in _starObjects)
                {
                    star.PulseGoldMeter();
                }
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
                    // Complete current star
                    _starObjects[_currentStar++].SetProgress(1);

                    // Show and complete any skipped stars
                    for (int i = _currentStar; i < topStar && i < _starObjects.Length; i++)
                    {
                        _starObjects[i].PopNew();
                        _starObjects[i].SetProgress(1);
                    }

                    // Show new star
                    _currentStar = topStar;
                    if (_currentStar < _starObjects.Length)
                        _starObjects[_currentStar].PopNew();

                    GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGain);
                    YargLogger.LogFormatDebug("Gained star {0} at score {1}", topStar, GameManager.BandScore);
                }

                if (_currentStar < _starObjects.Length)
                {
                    _starObjects[_currentStar].SetProgress(starProgress);
                }
            }

            if (stars is >= 5f and < 6f)
            {
                foreach (var star in _starObjects)
                {
                    star.SetGoldProgress(starProgress);
                }
            }
            else if (stars >= 6f)
            {
                foreach (var star in _starObjects)
                {
                    star.SetGoldProgress(1);
                }

                GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGold);
                _isGoldAchieved = true;
            }
        }
    }
}