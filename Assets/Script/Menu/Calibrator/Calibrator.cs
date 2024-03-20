using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using YARG.Audio;
using YARG.Core.Audio;
using YARG.Core.Input;
using YARG.Input;
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
            AudioWaiting,
            Audio,
            AudioDone
        }

        [SerializeField]
        private GameObject _startingStateContainer;

        [SerializeField]
        private GameObject _audioCalibrateContainer;

        [Space]
        [SerializeField]
        private TextMeshProUGUI _audioCalibrateText;

        private State _state = State.Starting;
        private readonly List<float> _calibrationTimes = new();

        private YargPlayer _player;
#nullable enable
        private StemMixer? _mixer;
#nullable disable

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
                case State.AudioWaiting:
                    _state = State.Audio;
                    UpdateForState();
                    break;
                case State.Audio:
                    _audioCalibrateText.color = Color.green;
                    _audioCalibrateText.text = "Detected";

                    _calibrationTimes.Add((float)_mixer.GetPosition());
                    break;
                case State.Starting:
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
            switch (_state)
            {
                case State.Audio:
                    // Fade out text
                    var color = _audioCalibrateText.color;
                    color.a -= Time.deltaTime * 3f;
                    _audioCalibrateText.color = color;
                    break;
            }
        }

        private void UpdateForState()
        {
            _mixer?.Dispose();
            _mixer = null;

            StopAllCoroutines();

            _startingStateContainer.SetActive(false);
            _audioCalibrateContainer.SetActive(false);

            switch (_state)
            {
                case State.Starting:
                    _startingStateContainer.SetActive(true);
                    break;
                case State.AudioWaiting:
                    _audioCalibrateContainer.SetActive(true);
                    _player = null;

                    _audioCalibrateText.color = Color.white;
                    _audioCalibrateText.text =
                        "Press any button on each tick you hear.\n" +
                        "Press any button when you are ready.";
                    break;
                case State.Audio:
                    _audioCalibrateContainer.SetActive(true);
                    _calibrationTimes.Clear();

                    var file = Path.Combine(Application.streamingAssetsPath, "calibration_music.ogg");
                    _mixer = GlobalAudioHandler.LoadCustomFile(file, 1f);
                    _mixer.Play();
                    StartCoroutine(AudioCalibrateCoroutine());
                    break;
                case State.AudioDone:
                    _audioCalibrateContainer.SetActive(true);
                    CalculateAudioLatency();
                    break;
            }
        }

        private void CalculateAudioLatency()
        {
            // Drop all discrepancies
            for (int i = _calibrationTimes.Count - 1; i > 1; i--)
            {
                if (Mathf.Abs(_calibrationTimes[i] - (_calibrationTimes[i - 1] + SECONDS_PER_BEAT)) > DROP_THRESH)
                {
                    _calibrationTimes.RemoveAt(i);
                }
            }

            // If there isn't enough data, RIP
            if (_calibrationTimes.Count <= 8)
            {
                _audioCalibrateText.color = Color.red;
                _audioCalibrateText.text =
                    "There isn't enough data to get an accurate result.\n" +
                    "Press back to exit.";
                return;
            }

            // Get the deviations
            var diffs = new List<float>();
            for (int i = 0; i < _calibrationTimes.Count; i++)
            {
                // Our goal is to get as close to 0 as possible
                float diff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * i);

                // Look forwards
                for (int j = 1;; j++)
                {
                    float newDiff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i + j));
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
                    float newDiff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i - j));
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
            float median = diffs.Count % 2 != 0 ? diffs[mid] : (diffs[mid] + diffs[mid - 1]) / 2f;

            // Set calibration
            int calibration = Mathf.RoundToInt(median * 1000f);
            SettingsManager.Settings.AudioCalibration.Value = calibration;

            // Set text
            _audioCalibrateText.color = Color.green;
            _audioCalibrateText.text =
                $"Calibration set to {calibration}ms!\n" +
                "Press back to exit.";
        }

        private IEnumerator AudioCalibrateCoroutine()
        {
            _audioCalibrateText.color = Color.white;
            _audioCalibrateText.text = "1";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 1f);
            _audioCalibrateText.color = Color.white;
            _audioCalibrateText.text = "2";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 2f);
            _audioCalibrateText.color = Color.white;
            _audioCalibrateText.text = "3";

            yield return new WaitUntil(() => _mixer.GetPosition() >= SECONDS_PER_BEAT * 3f);
            _audioCalibrateText.color = Color.white;
            _audioCalibrateText.text = "4";

            yield return new WaitUntil(() => !_mixer.IsPlaying);
            _state = State.AudioDone;
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
    }
}