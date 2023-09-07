using System;
using TMPro;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Menu;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public class ScoreBox : GameplayBehaviour
    {
        private const string SCORE_PREFIX = "<mspace=0.538em>";

        private const string TIME_FORMAT       = "m\\:ss";
        private const string TIME_FORMAT_HOURS = "h\\:mm\\:ss";

        [SerializeField]
        private TextMeshProUGUI _scoreText;
        [SerializeField]
        private StarDisplay _starDisplay;

        [Space]
        [SerializeField]
        private ProgressBarFadedEdge _songProgressBar;
        [SerializeField]
        private TextMeshProUGUI _songTimer;

        private int _bandScore;

        private bool _songHasHours;
        private string _songLengthTime;

        private string TimeFormat => _songHasHours ? TIME_FORMAT_HOURS : TIME_FORMAT;

        private void Start()
        {
            _scoreText.text = SCORE_PREFIX + "0";
            _songTimer.text = string.Empty;

            _songProgressBar.SetProgress(0f);
        }

        protected override void OnSongStarted()
        {
            var timeSpan = TimeSpan.FromSeconds(GameManager.SongLength);
            _songHasHours = timeSpan.TotalHours >= 1.0;
            _songLengthTime = timeSpan.ToString(TimeFormat);
        }

        private void Update()
        {
            // Update score
            if (GameManager.BandScore != _bandScore)
            {
                _bandScore = GameManager.BandScore;
                _scoreText.text = SCORE_PREFIX + _bandScore.ToString("N0");

                UpdateStars();
            }

            // Update song progress

            double time = Math.Max(0f, GameManager.SongTime);

            if (SettingsManager.Settings.GraphicalProgressOnScoreBox.Data)
            {
                _songProgressBar.SetProgress((float) (time / GameManager.SongLength));
            }

            // Skip if the song length has not been established yet, or if disabled
            if (_songLengthTime == null) return;

            string countUp = TimeSpan.FromSeconds(time).ToString(TimeFormat);
            string countDown = TimeSpan.FromSeconds(GameManager.SongLength - time).ToString(TimeFormat);

            _songTimer.text = SettingsManager.Settings.SongTimeOnScoreBox.Data switch
            {
                "CountUpAndTotal"   => $"{countUp} / {_songLengthTime}",
                "CountDownAndTotal" => $"{countDown} / {_songLengthTime}",
                "CountUpOnly"       => countUp,
                "CountDownOnly"     => countDown,
                "TotalOnly"         => _songLengthTime,
                _                   => string.Empty
            };
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