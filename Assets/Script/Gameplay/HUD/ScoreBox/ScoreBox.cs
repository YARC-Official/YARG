using System;
using Cysharp.Text;
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

        private const string TIME_FORMAT       = @"m\:ss";
        private const string TIME_FORMAT_HOURS = @"h\:mm\:ss";

        [SerializeField]
        private TextMeshProUGUI _scoreText;
        [SerializeField]
        private StarScoreDisplay _starScoreDisplay;

        [Space]
        [SerializeField]
        private ProgressBarFadedEdge _songProgressBar;
        [SerializeField]
        private TextMeshProUGUI _songTimer;

        private int _bandScore;

        private bool _songHasHours;
        private string _songLengthTime;
        private string _timeFormat;

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
            _timeFormat = _songHasHours ? TIME_FORMAT_HOURS : TIME_FORMAT;
            _songLengthTime = timeSpan.ToString(_timeFormat);

            _timeFormat = SettingsManager.Settings.SongTimeOnScoreBox.Value switch
            {
                SongProgressMode.CountUpAndTotal   => $"{{0:{_timeFormat}}} / {{2}}",
                SongProgressMode.CountDownAndTotal => $"{{1:{_timeFormat}}} / {{2}}",
                SongProgressMode.CountUpOnly       => $"{{0:{_timeFormat}}}",
                SongProgressMode.CountDownOnly     => $"{{1:{_timeFormat}}}",
                SongProgressMode.TotalOnly         => $"{{2:{_timeFormat}}}",

                _ => string.Empty
            };
        }

        private void Update()
        {
            // Update score
            if (GameManager.BandScore != _bandScore)
            {
                _bandScore = GameManager.BandScore;
                _scoreText.SetTextFormat("{0}{1:N0}", SCORE_PREFIX, _bandScore);

                _starScoreDisplay.SetStars(GameManager.BandStars);
            }

            // Update song progress

            double time = Math.Max(0f, GameManager.SongTime);

            if (SettingsManager.Settings.GraphicalProgressOnScoreBox.Value)
            {
                _songProgressBar.SetProgress((float) (time / GameManager.SongLength));
            }

            // Skip if the song length has not been established yet, or if disabled
            if (_songLengthTime == null) return;

            var countUp = TimeSpan.FromSeconds(time);
            var countDown = TimeSpan.FromSeconds(GameManager.SongLength - time);

            _songTimer.SetTextFormat(_timeFormat, countUp, countDown, _songLengthTime);
        }
    }
}