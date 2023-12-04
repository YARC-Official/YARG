using System;
using TMPro;
using UnityEngine;
using YARG.Settings;

namespace YARG.Gameplay.HUD
{
    public enum SongProgressMode
    {
        None,
        CountUpAndTotal,
        CountDownAndTotal,
        CountUpOnly,
        CountDownOnly,
        TotalOnly,
    }

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

                _starDisplay.SetStars(GameManager.BandStars);
            }

            // Update song progress

            double time = Math.Max(0f, GameManager.SongTime);

            if (SettingsManager.Settings.GraphicalProgressOnScoreBox.Value)
            {
                _songProgressBar.SetProgress((float) (time / GameManager.SongLength));
            }

            // Skip if the song length has not been established yet, or if disabled
            if (_songLengthTime == null) return;

            string countUp = TimeSpan.FromSeconds(time).ToString(TimeFormat);
            string countDown = TimeSpan.FromSeconds(GameManager.SongLength - time).ToString(TimeFormat);

            _songTimer.text = SettingsManager.Settings.SongTimeOnScoreBox.Value switch
            {
                SongProgressMode.CountUpAndTotal   => $"{countUp} / {_songLengthTime}",
                SongProgressMode.CountDownAndTotal => $"{countDown} / {_songLengthTime}",
                SongProgressMode.CountUpOnly       => countUp,
                SongProgressMode.CountDownOnly     => countDown,
                SongProgressMode.TotalOnly         => _songLengthTime,

                _ => string.Empty
            };
        }
    }
}