using System;
using System.Linq;
using Cysharp.Text;
using DG.Tweening;
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
        private GameObject _bandMultiplierObject;
        [SerializeField]
        private TextMeshProUGUI _bandMultiplierText;

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
        [SerializeField]
        private Image _bandMultiplierBackgroundImage;

        [Space]
        [SerializeField]
        private int _characterCountForBreak;
        [SerializeField]
        private Sprite _brokenBackgroundSprite;
        [SerializeField]
        private Sprite _brokenOverlaySprite;

        private int _bandScore;
        private int _bandCombo;
        private int _bandMultiplier;

        private bool _songHasHours;
        private string _songLengthTime;
        private string _timeFormat;

        private bool _easterEggTriggered;
        // Default to 1 to prevent divide-by-zero when disabled
        private int _bandComboUnits = 1;
        private bool _singlePlayer;
        private int _displayedCountUpSeconds = -1;
        private int _displayedCountDownSeconds = -1;

        private Tween _multiplierShowTweener;

        protected override void GameplayAwake()
        {
            _multiplierShowTweener =
                DOTween.Sequence()
                .Append(
                    _bandMultiplierObject.transform
                    .DOScaleX(1f, 0.5f)
                    .SetEase(Ease.OutBack)
                )
                .Join(
                    _bandMultiplierBackgroundImage.DOFade(1f, 0.5f)
                )
                .SetAutoKill(false)
                .Pause()
                .SetLink(gameObject);
        }

        private void Start()
        {
            _scoreText.text = SCORE_PREFIX + "0";
            _bandComboText.text = SCORE_PREFIX + "0";
            _songTimer.text = string.Empty;
            _displayedCountUpSeconds = -1;
            _displayedCountDownSeconds = -1;

            _songProgressBar.SetProgress(0f);
        }

        protected override void OnChartLoaded(SongChart chart)
        {
            _bandComboObject.SetActive(SettingsManager.Settings.BandComboTypeSetting.Value != BandComboType.Off);
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

            // This is here because in the other init functions, GameManager.Players is not yet defined.
            if (_bandComboObject.activeSelf)             {
                _bandComboUnits = GameManager.Players.Min(e => e.BaseStats.BandComboUnits);
            }
            _singlePlayer = GameManager.Players.Count == 1;
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
                _bandComboText.SetTextFormat("{0}{1:N0}", SCORE_PREFIX, _bandCombo / _bandComboUnits);
            }

            UpdateBandMultiplier();

            // Update song progress
            double length = GameManager.SongLength / GameManager.SongSpeed;
            double time = Math.Clamp(GameManager.SongTime / GameManager.SongSpeed, 0f, length);

            if (SettingsManager.Settings.GraphicalProgressOnScoreBox.Value)
            {
                _songProgressBar.SetProgress((float) (time / length));
            }

            // Skip if the song length has not been established yet, or if disabled
            if (_songLengthTime == null)
            {
                return;
            }

            var countUpSeconds = (int) time;
            var countDownSeconds = (int) (length - time);
            if (countUpSeconds != _displayedCountUpSeconds || countDownSeconds != _displayedCountDownSeconds)
            {
                var countUp = TimeSpan.FromSeconds(countUpSeconds);
                var countDown = TimeSpan.FromSeconds(countDownSeconds);
                _songTimer.SetTextFormat(_timeFormat, countUp, countDown, _songLengthTime);
                _displayedCountUpSeconds = countUpSeconds;
                _displayedCountDownSeconds = countDownSeconds;
            }
        }

        private void UpdateBandMultiplier()
        {
            if (GameManager.BandMultiplier == _bandMultiplier)
            {
                return;
            }

            var show = GameManager.BandMultiplier > 1 && !_singlePlayer;
            _bandMultiplier = GameManager.BandMultiplier;
            _bandMultiplierText.SetTextFormat("{0}x", GameManager.BandMultiplier);

            if (show)
            {
                _multiplierShowTweener.PlayForward();
            }
            else
            {
                _multiplierShowTweener.PlayBackwards();
            }
        }
    }
}