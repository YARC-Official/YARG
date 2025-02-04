using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Input;
using YARG.Localization;
using YARG.Player;
using YARG.Settings;

namespace YARG.Menu.Calibrator
{
    // TODO: Redo this

    public class Calibrator : MonoBehaviour
    {
        private const float SECONDS_PER_BEAT = 1f / 80f * 60f;
        private const float DROP_THRESH = 0.05f;

        private enum State
        {
            Starting,
            VideoWaiting,
            Video,
            VideoDone,
            AudioWaiting,
            Audio,
            AudioDone
        }

        [SerializeField]
        private GameObject _startingStateContainer;

        [SerializeField]
        private GameObject _calibrateContainer;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _infoText;

        [SerializeField]
        private TextMeshProUGUI _detectedText;

        private State _state = State.Starting;
        private readonly List<double> _calibrationTimes = new();

        private YargPlayer _player;
#nullable enable
        private StemMixer? _mixer;
        private double _time;
#nullable disable
        private uint _timerCount = 0;

        private void Start()
        {
            UpdateForState();

            InputManager.MenuInput += OnMenuInput;
        }

        private void OnDestroy()
        {
            InputManager.MenuInput -= OnMenuInput;
            _mixer?.Dispose();
        }

        private void OnMenuInput(YargPlayer player, ref GameInput input)
        {
            // Only detect button downs
            if (!input.Button) return;

            // Only allow inputs from one player at a time
            if (_player is null)
            {
                _player = player;
            }
            else if (_player != player)
            {
                return;
            }

            switch (_state)
            {
                case State.VideoWaiting:
                    _state = State.Video;
                    UpdateForState();
                    break;
                case State.AudioWaiting:
                    _state = State.Audio;
                    UpdateForState();
                    break;
                case State.Video:
                case State.Audio:
                    _calibrationTimes.Add(Time.realtimeSinceStartupAsDouble - _time);

                    _detectedText.color = Color.green;
                    _detectedText.text = Localize.Key("Menu.Calibrator.Detected");
                    break;
                case State.Starting:
                case State.VideoDone:
                case State.AudioDone:
                    if (input.GetAction<MenuAction>() == MenuAction.Red)
                    {
                        BackButton();
                    }
                    break;
            }
        }

        private void Update()
        {
            var color = Color.white;
            switch (_state)
            {
                case State.Video:
                    // Fade out text
                    color = _infoText.color;
                    color.a -= Time.deltaTime * 9f;
                    _infoText.color = color;

                    color = _detectedText.color;
                    color.a -= Time.deltaTime * 3f;
                    _detectedText.color = color;
                    break;
                case State.Audio:
                    // Fade out text
                    color = _infoText.color;
                    color.a -= Time.deltaTime * 3f;
                    _infoText.color = color;

                    color = _detectedText.color;
                    color.a -= Time.deltaTime * 3f;
                    _detectedText.color = color;
                    break;
            }
        }

        private void UpdateForState()
        {
            _mixer?.Dispose();
            _mixer = null;

            StopAllCoroutines();
            CancelInvoke();

            _startingStateContainer.SetActive(false);
            _calibrateContainer.SetActive(false);

            _infoText.fontSize = 64;

            int calibration = -10000;

            switch (_state)
            {
                case State.Starting:
                    _startingStateContainer.SetActive(true);
                    break;

                case State.VideoWaiting:
                    _calibrateContainer.SetActive(true);
                    _player = null;

                    _infoText.color = Color.white;
                    _infoText.text =
                        "Press any button on each circle you see.\n" +
                        "Press any button when you are ready.";
                    break;
                case State.Video:
                    _calibrateContainer.SetActive(true);
                    _calibrationTimes.Clear();

                    _timerCount = 0;
                    _time = Time.realtimeSinceStartupAsDouble;
                    InvokeRepeating("VideoCalibrationTimer", SECONDS_PER_BEAT, SECONDS_PER_BEAT);
                    break;
                case State.VideoDone:
                    _calibrateContainer.SetActive(true);
                    _detectedText.text = "";

                    calibration = CalculateLatency();
                    if (calibration > -10000)
                    {
                        SettingsManager.Settings.VideoCalibration.Value = calibration;

                        // Set text
                        _infoText.color = Color.green;
                        _infoText.text =
                            $"Calibration set to {calibration}ms!\n" +
                            "Press back to exit.";
                    }
                    break;

                case State.AudioWaiting:
                    _calibrateContainer.SetActive(true);
                    _player = null;

                    _infoText.color = Color.white;
                    _infoText.text =
                        "Press any button on each tick you hear.\n" +
                        "Press any button when you are ready.";
                    break;
                case State.Audio:
                    _calibrateContainer.SetActive(true);
                    _calibrationTimes.Clear();

                    const float SPEED = 1f;
                    const double VOLUME = 1.0;
                    var file = Path.Combine(Application.streamingAssetsPath, "calibration_music.ogg");
                    _mixer = GlobalAudioHandler.LoadCustomFile(file, SPEED, VOLUME);
                    _mixer.SongEnd += OnAudioEnd;
                    _mixer.Play(true);
                    _time = Time.realtimeSinceStartupAsDouble;
                    StartCoroutine(AudioCalibrateCoroutine());
                    break;
                case State.AudioDone:
                    _calibrateContainer.SetActive(true);
                    _detectedText.text = "";

                    calibration = CalculateLatency();
                    if (calibration > -10000)
                    {
                        if (SettingsManager.Settings.AccountForHardwareLatency.Value)
                            calibration -= GlobalAudioHandler.PlaybackLatency;
                        SettingsManager.Settings.AudioCalibration.Value = calibration;

                        // Set text
                        _infoText.color = Color.green;
                        _infoText.text =
                            $"Calibration set to {calibration}ms!\n" +
                            "Press back to exit.";
                    }
                    break;
            }
        }

        private int CalculateLatency()
        {
            // Drop all discrepancies
            for (int i = _calibrationTimes.Count - 1; i > 1; i--)
            {
                if (Math.Abs(_calibrationTimes[i] - (_calibrationTimes[i - 1] + SECONDS_PER_BEAT)) > DROP_THRESH)
                {
                    _calibrationTimes.RemoveAt(i);
                }
            }

            // If there isn't enough data, RIP
            if (_calibrationTimes.Count <= 8)
            {
                _infoText.color = Color.red;
                _infoText.text = Localize.Key("Menu.Calibrator.NotEnoughData");
                return -10000;
            }

            // Get the deviations
            var diffs = new List<double>();
            for (int i = 0; i < _calibrationTimes.Count; i++)
            {
                // Our goal is to get as close to 0 as possible
                double diff = Math.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * i);

                // Look forwards
                for (int j = 1;; j++)
                {
                    double newDiff = Math.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i + j));
                    if (newDiff < diff)
                    {
                        diff = newDiff;
                    }
                    else
                    {
                        break;
                    }
                }

                // Look backwards
                for (int j = 1;; j++)
                {
                    double newDiff = Math.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i - j));
                    if (newDiff < diff)
                    {
                        diff = newDiff;
                    }
                    else
                    {
                        break;
                    }
                }

                diffs.Add(diff);
            }

            // Get the median
            diffs.Sort();
            int mid = diffs.Count / 2;
            double median = diffs.Count % 2 != 0 ? diffs[mid] : (diffs[mid] + diffs[mid - 1]) / 2f;

            // return median
            return (int)Math.Round(median * 1000);
        }

        private void VideoCalibrationTimer()
        {
            _infoText.fontSize = 64;
            _infoText.color = Color.white;
            switch (_timerCount)
            {
                case 0:
                    _infoText.text = "4";
                    break;
                case 1:
                    _infoText.text = "3";
                    break;
                case 2:
                    _infoText.text = "2";
                    break;
                case 3:
                    _infoText.text = "1";
                    break;
                case < 20:
                    _infoText.fontSize = 256;
                    // black circle
                    _infoText.text = "\u25CF";
                    break;
                default:
                    _state = State.VideoDone;
                    UpdateForState();
                    break;
            }
            ++_timerCount;
        }

        private IEnumerator AudioCalibrateCoroutine()
        {
            _infoText.color = Color.white;
            _infoText.text = "4";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 1f);
            _infoText.color = Color.white;
            _infoText.text = "3";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 2f);
            _infoText.color = Color.white;
            _infoText.text = "2";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 3f);
            _infoText.color = Color.white;
            _infoText.text = "1";
        }

        public void StartVideoMode()
        {
            _state = State.VideoWaiting;
            UpdateForState();
        }

        public void StartAudioMode()
        {
            _state = State.AudioWaiting;
            UpdateForState();
        }

        public void BackButton()
        {
            if (_state == State.Starting)
            {
                GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
            }
            else
            {
                _state = State.Starting;
                UpdateForState();
            }
        }

        private void OnAudioEnd()
        {
            _state = State.AudioDone;
            UpdateForState();
        }
    }
}