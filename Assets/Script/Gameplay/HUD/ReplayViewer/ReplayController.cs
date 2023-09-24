using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using YARG.Core.Input;
using YARG.Core.Replays;
using YARG.Menu;
using YARG.Menu.Navigation;

namespace YARG.Gameplay.HUD
{
    public class ReplayController : GameplayBehaviour
    {
        private const float SLIDER_COOLDOWN = 0.5f;

        [SerializeField]
        private DragSlider _timeSlider;

        [SerializeField]
        private TMP_InputField _speedInput;

        [SerializeField]
        private TMP_InputField _timeInput;

        [SerializeField]
        private TextMeshProUGUI _songLengthText;

        [SerializeField]
        private float _hudAnimationTime;

        private RectTransform _rectTransform;

        private Replay _replay;

        private float _sliderValue;
        private float _sliderTimeInactive;
        private float _hudHiddenY;

        private bool _sliderChanged;
        private bool _hudVisible;

        protected override void GameplayAwake()
        {
            if (!GameManager.IsReplay)
            {
                Destroy(gameObject);
                return;
            }

            _rectTransform = GetComponent<RectTransform>();
            _hudHiddenY = transform.position.y;

            _timeSlider.OnSliderDrag.AddListener(OnTimeSliderDragged);

            // Listen for menu inputs
            Navigator.Instance.NavigationEvent += OnNavigationEvent;
        }

        protected override void GameplayDestroy()
        {
            _timeSlider.OnSliderDrag.RemoveAllListeners();
        }

        protected override void OnSongLoaded()
        {
            _songLengthText.text = TimeSpan.FromSeconds(GameManager.SongLength + GameManager.SONG_START_DELAY)
                .ToString("g");

            _replay = GameManager.Replay;
        }

        private void Update()
        {
            if (!GameManager.Paused && _hudVisible)
            {
                UpdateReplayUI(true, false);
            }

            if (_sliderChanged)
            {
                _sliderTimeInactive += Time.deltaTime;
                if (_sliderTimeInactive >= SLIDER_COOLDOWN)
                {
                    _sliderChanged = false;
                    _sliderTimeInactive = 0f;
                    SetReplayTime(_timeSlider.value * GameManager.SongLength);
                }
            }
        }

        private void UpdateReplayUI(bool updateSlider, bool isDragging, double time = 0)
        {
            if (isDragging)
            {
                _timeInput.text = TimeSpan.FromSeconds(time).ToString("g");
            }
            else
            {
                _timeInput.text = TimeSpan.FromSeconds(GameManager.InputTime + GameManager.SONG_START_DELAY)
                    .ToString("g");
            }

            if (updateSlider)
            {
                double value;
                if (_sliderChanged)
                {
                    value = _sliderValue * GameManager.SongLength;
                }
                else
                {
                    value = GameManager.InputTime / GameManager.SongLength;
                }

                _timeSlider.SetValueWithoutNotify((float) value);
            }
        }

        private void SetReplayTime(double time)
        {
            Debug.Log("Set replay time to " + time);

            foreach (var player in GameManager.Players)
            {
                player.SetReplayTime(time);
            }

            GameManager.SetSongTime(time, 0);
            GameManager.OverridePauseTime(GameManager.RealInputTime);
        }

        public void OnTimeSliderDragged(float value)
        {
            UpdateReplayUI(false, true, (value * GameManager.SongLength));
        }

        public void OnTimeSliderChanged(float value)
        {
            if (!GameManager.Paused)
            {
                GameManager.Pause(false);
            }

            _sliderTimeInactive = 0f;
            _sliderChanged = true;

            UpdateReplayUI(false, false);
        }

        public void TogglePause()
        {
            if (!GameManager.Paused)
            {
                GameManager.Pause(false);
            }
            else
            {
                GameManager.Resume();
            }
        }

        public void AdjustSpeed(float adjustment)
        {
            GameManager.SetSongSpeed(GameManager.SelectedSongSpeed + adjustment);

            _speedInput.text = $"{GameManager.SelectedSongSpeed * 100f:0}%";
        }

        public void ToggleHUD()
        {
            if (_hudVisible)
            {
                // Hide hud (make sure to use unscaled time)
                _rectTransform
                    .DOMoveY(_hudHiddenY, _hudAnimationTime)
                    .SetUpdate(true);
            }
            else
            {
                // Show hud (make sure to use unscaled time)
                _rectTransform
                    .DOMoveY(0f, _hudAnimationTime)
                    .SetEase(Ease.InOutQuint)
                    .SetUpdate(true);
            }

            _hudVisible = !_hudVisible;
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