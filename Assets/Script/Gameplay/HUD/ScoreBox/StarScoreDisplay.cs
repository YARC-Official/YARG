using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Logging;
using YARG.Playback;

namespace YARG.Gameplay.HUD
{
    public class StarScoreDisplay : GameplayBehaviour
    {
        [SerializeField]
        private StarDisplay[] _starObjects;

        private int  _currentStar;
        private bool _isGoldAchieved;

        protected override void OnChartLoaded(SongChart chart)
        {
            _starObjects[0].PopNew();
        }

        private void Update()
        {
            if (_currentStar == 5 && !_isGoldAchieved)
            {
                float pulse = 1 - (float) ((GameManager.BeatEventHandler.Visual.StrongBeat.CurrentProgress / 2) % 1);
                foreach (var star in _starObjects)
                {
                    star.SetGoldPulse(pulse);
                }
            }
        }

        public void SetStars(float stars)
        {
            int topStar = (int) stars;
            float starProgress = stars - topStar;

            // Revert gold state if the score has dropped back below gold threshold
            if (_isGoldAchieved && stars < 6f)
            {
                _isGoldAchieved = false;
            }

            if (topStar < _currentStar)
            {
                for (int i = _currentStar; i > topStar; i--)
                {
                    if (i < _starObjects.Length)
                    {
                        _starObjects[i].HideStar();
                    }
                }

                _currentStar = topStar;

                if (_currentStar < _starObjects.Length)
                {
                    _starObjects[_currentStar].PopNew();
                }
            }
            else if (topStar > _currentStar)
            {
                if (_currentStar < _starObjects.Length)
                {
                    _starObjects[_currentStar].SetProgress(1);
                }

                _currentStar++;

                // Show and complete any skipped stars
                for (int i = _currentStar; i < topStar && i < _starObjects.Length; i++)
                {
                    _starObjects[i].PopNew();
                    _starObjects[i].SetProgress(1);
                }

                // Show new star
                _currentStar = topStar;
                if (_currentStar < _starObjects.Length)
                {
                    _starObjects[_currentStar].PopNew();
                }

                GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGain);
                YargLogger.LogFormatDebug("Gained star {0} at score {1}", topStar, GameManager.BandScore);
            }

            if (_currentStar < _starObjects.Length)
            {
                _starObjects[_currentStar].SetProgress(starProgress);
            }

            if (stars >= 6f)
            {
                foreach (var star in _starObjects)
                {
                    star.SetGoldProgress(1);
                }

                if (!_isGoldAchieved)
                {
                    GlobalAudioHandler.PlaySoundEffect(SfxSample.StarGold);
                    _isGoldAchieved = true;
                }
            }
            else if (stars >= 5f)
            {
                foreach (var star in _starObjects)
                {
                    star.SetGoldProgress(starProgress);
                }
            }
            else
            {
                foreach (var star in _starObjects)
                {
                    star.SetGoldProgress(0);
                }
            }
        }
    }
}