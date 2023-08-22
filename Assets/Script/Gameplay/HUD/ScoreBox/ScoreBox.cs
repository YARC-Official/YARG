using System;
using TMPro;
using UnityEngine;
using YARG.Menu;

namespace YARG.Gameplay.HUD
{
    public class ScoreBox : GameplayBehaviour
    {
        private const string SCORE_PREFIX = "<mspace=0.538em>";

        [SerializeField]
        private TextMeshProUGUI _scoreText;
        [SerializeField]
        private ProgressBarFadedEdge _songProgressBar;
        [SerializeField]
        private StarDisplay _starDisplay;

        private int _bandScore;

        private void Start()
        {
            _scoreText.text = SCORE_PREFIX + "0";
        }

        private void Update()
        {
            if (GameManager.Paused)
            {
                return;
            }

            if (GameManager.BandScore != _bandScore)
            {
                _bandScore = GameManager.BandScore;
                _scoreText.text = SCORE_PREFIX + _bandScore.ToString("N0");

                UpdateStars();
            }

            _songProgressBar.SetProgress((float) (GameManager.SongTime / GameManager.SongLength));
        }

        private void UpdateStars()
        {
            double totalStarCount = 0;

            foreach (var player in GameManager.Players)
            {
                if (player.StarScoreThresholds[0] == 0)
                {
                    continue;
                }

                int fullStars = 5;
                while (fullStars >= 0 && player.Score < player.StarScoreThresholds[fullStars])
                {
                    fullStars--;
                }

                fullStars++;

                totalStarCount += fullStars;

                double progressToNextStar = 0;
                if (fullStars == 0)
                {
                    progressToNextStar = player.Score / (double)player.StarScoreThresholds[fullStars];
                }
                else if (fullStars < 6)
                {
                    int previousStarThreshold = player.StarScoreThresholds[fullStars - 1];
                    progressToNextStar = (player.Score - previousStarThreshold) /
                        (double)(player.StarScoreThresholds[fullStars] - previousStarThreshold);
                }

                totalStarCount += progressToNextStar;
            }

            _starDisplay.SetStars(totalStarCount);
        }
    }
}