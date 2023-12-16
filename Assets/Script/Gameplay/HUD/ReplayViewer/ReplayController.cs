using System;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class ReplayController : GameplayBehaviour
    {
        private const string TIME_FORMATTING = @"h\:mm\:ss\.fff";

        [SerializeField]
        private RectTransform _container;
        [SerializeField]
        private RectTransform _showHudButtonArrow;

        [Space]
        [SerializeField]
        private GameObject _playButton;
        [SerializeField]
        private GameObject _pauseButton;
        [SerializeField]
        private TMP_InputField _timeInput;
        [SerializeField]
        private TMP_InputField _speedInput;
        [SerializeField]
        private TextMeshProUGUI _songLengthText;
        [SerializeField]
        private Slider _timelineSlider;

        [Space]
        [SerializeField]
        private float _hudAnimationTime;

        private Replay _replay;

        private float _hudHiddenY;
        private bool _hudVisible;

        protected override void GameplayAwake()
        {
            if (!GameManager.IsReplay)
            {
                Destroy(gameObject);
                return;
            }

            // Get the hidden position based on the container, and then move to that position
            _hudHiddenY = -_container.sizeDelta.y;
            _container.position = _container.position.WithY(_hudHiddenY);

            // Listen for menu inputs
            Navigator.Instance.NavigationEvent += OnNavigationEvent;
        }

        protected override void GameplayDestroy()
        {
        }

        protected override void OnSongLoaded()
        {
            _songLengthText.text = " / " + TimeSpan
                .FromSeconds(GameManager.SongLength + GameManager.SONG_START_DELAY)
                .ToString(TIME_FORMATTING);

            _replay = GameManager.Replay;
        }

        private void Update()
        {
            if (!_hudVisible) return;

            if (!GameManager.Paused)
            {
                UpdateTimeControls();
            }
        }

        private void UpdateTimeControls()
        {
            _timeInput.text = TimeSpan
                .FromSeconds(GameManager.VisualTime + GameManager.SONG_START_DELAY)
                .ToString(TIME_FORMATTING);

            _timelineSlider.value = Mathf.Clamp01((float) (GameManager.SongTime / GameManager.SongLength));
        }

        public void ToggleHUD()
        {
            if (_hudVisible)
            {
                // Hide hud (make sure to use unscaled time)
                _container
                    .DOMoveY(_hudHiddenY, _hudAnimationTime)
                    .SetEase(Ease.OutQuint)
                    .SetUpdate(true);

                _showHudButtonArrow.rotation = Quaternion.Euler(0f, 0f, 180f);
            }
            else
            {
                // Show hud (make sure to use unscaled time)
                _container
                    .DOMoveY(0f, _hudAnimationTime)
                    .SetEase(Ease.OutQuint)
                    .SetUpdate(true);

                _showHudButtonArrow.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            _hudVisible = !_hudVisible;
        }

        public void TogglePause()
        {
            if (!GameManager.Paused)
            {
                GameManager.Pause(false);

                _playButton.gameObject.SetActive(true);
                _pauseButton.gameObject.SetActive(false);
            }
            else
            {
                GameManager.Resume();

                _playButton.gameObject.SetActive(false);
                _pauseButton.gameObject.SetActive(true);
            }
        }

        public void ForcePause()
        {
            // Force pause
            if (!GameManager.Paused)
            {
                TogglePause();
            }
        }

        public void OnTimeInputEndEdit()
        {
            if (TimeSpan.TryParse(_timeInput.text, out var timeSpan))
            {
                // Prevent the audio from going out of bounds
                var newTime = Math.Clamp(timeSpan.TotalSeconds, 0, GameManager.SongLength);

                // Make sure to correct for the start delay
                SetReplayTime(newTime - GameManager.SONG_START_DELAY);
            }

            UpdateTimeControls();
        }

        public void OnTimelineSliderPointerUp()
        {
            SetReplayTime(GameManager.SongLength * _timelineSlider.value);

            // Once user lets go, play again.
            // The replay is guaranteed to be paused at this point, as the
            // pointer down even should call ForcePause.
            TogglePause();
        }

        public void OnSpeedInputEndEdit()
        {
            if (!int.TryParse(_speedInput.text.TrimEnd('%'), NumberStyles.Number, null, out int speed))
            {
                speed = 100;
            }

            // Make sure to reset the replay time to prevent inconsistencies
            GameManager.SetSongSpeed(speed / 100f);
            SetReplayTime(GameManager.VisualTime);

            _speedInput.text = $"{GameManager.SelectedSongSpeed * 100f:0}%";
        }

        private void SetReplayTime(double time)
        {
            Debug.Log("Set replay time to " + time);

            foreach (var player in GameManager.Players)
            {
                player.SetReplayTime(time);
            }

            GameManager.SetSongTime(time, 0);
            GameManager.OverridePauseTime();
        }

        private void OnNavigationEvent(NavigationContext context)
        {
            if (context.Action == MenuAction.Select)
            {
                ToggleHUD();
            }
        }
    }
}