using System;
using System.Linq;
using Cysharp.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Engine;
using YARG.Player;
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
        private GameObject _bandComboObject;

        [SerializeField]
        private TextMeshProUGUI _bandComboText;
        [SerializeField]
        private StarScoreDisplay _starScoreDisplay;

        [Space]
        [SerializeField]
        private ProgressBarFadedEdge _songProgressBar;
        [SerializeField]
        private TextMeshProUGUI _songTimer;

        [Space]
        [SerializeField]
        private Image _backgroundImage;
        [SerializeField]
        private Image _overlayImage;

        [Space]
        [SerializeField]
        private int _characterCountForBreak;
        [SerializeField]
        private Sprite _brokenBackgroundSprite;
        [SerializeField]
        private Sprite _brokenOverlaySprite;

        private int _bandScore;
        private int _bandCombo;
        
        private bool _songHasHours;
        private string _songLengthTime;
        private string _timeFormat;

        private bool _easterEggTriggered;
        private bool _vocalsOnly;

        private void Start()
        {
            _scoreText.text = SCORE_PREFIX + "0";
            _bandComboText.text = SCORE_PREFIX + "0";
            _songTimer.text = string.Empty;

            _songProgressBar.SetProgress(0f);
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _bandComboObject.SetActive(SettingsManager.Settings.BandComboTypeSetting.Value != BandComboType.Off);
            _vocalsOnly = PlayerContainer.Players.All(e => e.SittingOut || e.Profile.GameMode == GameMode.Vocals);
        }

        protected override void OnSongStarted()
        {
            var timeSpan = TimeSpan.FromSeconds(GameManager.SongLength / GameManager.SongSpeed);

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

                var scoreTextLength = _bandScore == 0 ? 1 : Math.Floor(Math.Log10(_bandScore) + 1);
                scoreTextLength += Math.Floor((scoreTextLength - 1) / 3); // thousand coma separators


                _starScoreDisplay.SetStars(GameManager.BandStars);

                // Trigger easter egg
                if (!_easterEggTriggered && scoreTextLength > _characterCountForBreak)
                {
                    _backgroundImage.sprite = _brokenBackgroundSprite;
                    _overlayImage.sprite = _brokenOverlaySprite;

                    _easterEggTriggered = true;
                }
            }

            if (GameManager.BandCombo != _bandCombo)
            {
                _bandCombo = GameManager.BandCombo;
                var modifier = _vocalsOnly ? 10 : 1;
                _bandComboText.SetTextFormat("{0}{1:N0}", SCORE_PREFIX, _bandCombo / modifier);
            }

            // Update song progress
            double length = GameManager.SongLength / GameManager.SongSpeed;
            double time = Math.Clamp(GameManager.SongTime / GameManager.SongSpeed, 0f, length);

            if (SettingsManager.Settings.GraphicalProgressOnScoreBox.Value)
            {
                _songProgressBar.SetProgress((float) (time / length));
            }

            // Skip if the song length has not been established yet, or if disabled
            if (_songLengthTime == null) return;

            var countUp = TimeSpan.FromSeconds(time);
            var countDown = TimeSpan.FromSeconds(length - time);

            _songTimer.SetTextFormat(_timeFormat, countUp, countDown, _songLengthTime);
        }
    }
}
