using System;
using TMPro;
using UnityEngine;
using YARG.Core.Replays;
using YARG.Menu;

namespace YARG.Gameplay.ReplayViewer
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

        private Replay _replay;

        private float _sliderValue;
        private float _sliderTimeInactive;
        private bool  _sliderChanged;

        protected override void GameplayAwake()
        {
            _timeSlider.OnSliderDrag.AddListener(OnTimeSliderDragged);
        }

        protected override void GameplayDestroy()
        {
            _timeSlider.OnSliderDrag.RemoveAllListeners();
        }

        protected override void OnSongLoaded()
        {
            if (!GameManager.IsReplay)
            {
                Destroy(gameObject);
                return;
            }

            _songLengthText.text = TimeSpan.FromSeconds(GameManager.SongLength + GameManager.SONG_START_DELAY).ToString("g");

            _replay = GameManager.Replay;
        }

        private void Update()
        {
            if (!GameManager.Paused)
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
                _timeInput.text = TimeSpan.FromSeconds(GameManager.InputTime + GameManager.SONG_START_DELAY).ToString("g");
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

            GameManager.SetSongTime(time);
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
    }
}