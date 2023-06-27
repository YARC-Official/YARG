using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Input;
using YARG.Settings;

namespace YARG {
	public class Calibrator : MonoBehaviour {
		private const float SECONDS_PER_BEAT = 1f / 80f * 60f;
		private const float DROP_THRESH = 0.05f;

		private enum State {
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
		private List<float> _calibrationTimes = new();

		private void Start() {
			UpdateForState();

			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy == null) {
					continue;
				}

				player.inputStrategy.GenericCalibrationEvent += OnGenericCalibrationEvent;
			}
		}

		private void OnDestroy() {
			foreach (var player in PlayerManager.players) {
				if (player.inputStrategy == null) {
					continue;
				}

				player.inputStrategy.GenericCalibrationEvent -= OnGenericCalibrationEvent;
			}
		}

		private void OnGenericCalibrationEvent(InputStrategy inputStrategy) {
			switch (_state) {
				case State.AudioWaiting:
					_state = State.Audio;
					UpdateForState();
					break;
				case State.Audio:
					_audioCalibrateText.color = Color.green;
					_audioCalibrateText.text = "STRUMMED";

					_calibrationTimes.Add(GameManager.AudioManager.CurrentPositionF);
					break;
			}
		}

		private void Update() {
			switch (_state) {
				case State.Audio:
					// Fade out text
					var color = _audioCalibrateText.color;
					color.a -= Time.deltaTime * 3f;
					_audioCalibrateText.color = color;
					break;
			}
		}

		private void UpdateForState() {
			GameManager.AudioManager.UnloadSong();
			StopAllCoroutines();

			_startingStateContainer.SetActive(false);
			_audioCalibrateContainer.SetActive(false);

			switch (_state) {
				case State.Starting:
					_startingStateContainer.SetActive(true);
					break;
				case State.AudioWaiting:
					_audioCalibrateContainer.SetActive(true);
					_audioCalibrateText.color = Color.white;
					_audioCalibrateText.text = "Strum/hit on each tick you hear. Strum/hit when you are ready.";
					break;
				case State.Audio:
					_audioCalibrateContainer.SetActive(true);
					_calibrationTimes.Clear();

					GameManager.AudioManager.LoadCustomAudioFile(Path.Combine(Application.streamingAssetsPath, "calibration_music.ogg"), 1f);
					GameManager.AudioManager.Play();
					StartCoroutine(AudioCalibrateCoroutine());
					break;
				case State.AudioDone:
					_audioCalibrateContainer.SetActive(true);
					CalculateAudioLatency();
					break;
			}
		}

		private void CalculateAudioLatency() {
			// Drop all discrepancies
			for (int i = _calibrationTimes.Count - 1; i > 1; i--) {
				if (Mathf.Abs(_calibrationTimes[i] - (_calibrationTimes[i - 1] + SECONDS_PER_BEAT)) > DROP_THRESH) {
					_calibrationTimes.RemoveAt(i);
				}
			}

			// If there isn't enough data, RIP
			if (_calibrationTimes.Count <= 8) {
				_audioCalibrateText.color = Color.red;
				_audioCalibrateText.text = "There isn't enough data to get an accurate result.";
				return;
			}

			// Get the deviations
			var diffs = new List<float>();
			for (int i = 0; i < _calibrationTimes.Count; i++) {
				// Our goal is to get as close to 0 as possible
				float diff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * i);

				// Look forwards
				for (int j = 1; ; j++) {
					float newDiff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i + j));
					if (newDiff < diff) {
						diff = newDiff;
					} else {
						break;
					}
				}

				// Look backwards
				for (int j = 1; ; j++) {
					float newDiff = Mathf.Abs(_calibrationTimes[i] - SECONDS_PER_BEAT * (i - j));
					if (newDiff < diff) {
						diff = newDiff;
					} else {
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
			SettingsManager.Settings.AudioCalibration.Data = calibration;

			// Set text
			_audioCalibrateText.color = Color.green;
			_audioCalibrateText.text = $"Calibration set to {calibration}ms!\nYou can now exit the calibrator.";
		}

		private IEnumerator AudioCalibrateCoroutine() {
			_audioCalibrateText.color = Color.white;
			_audioCalibrateText.text = "1";

			yield return new WaitUntil(() => GameManager.AudioManager.CurrentPositionF >= SECONDS_PER_BEAT * 1f);
			_audioCalibrateText.color = Color.white;
			_audioCalibrateText.text = "2";

			yield return new WaitUntil(() => GameManager.AudioManager.CurrentPositionF >= SECONDS_PER_BEAT * 2f);
			_audioCalibrateText.color = Color.white;
			_audioCalibrateText.text = "3";

			yield return new WaitUntil(() => GameManager.AudioManager.CurrentPositionF >= SECONDS_PER_BEAT * 3f);
			_audioCalibrateText.color = Color.white;
			_audioCalibrateText.text = "4";

			yield return new WaitUntil(() => GameManager.AudioManager.CurrentPositionF >= GameManager.AudioManager.AudioLengthF);
			_state = State.AudioDone;
			UpdateForState();
		}

		public void StartAudioMode() {
			_state = State.AudioWaiting;
			UpdateForState();
		}

		public void BackButton() {
			if (_state == State.Starting) {
				GameManager.Instance.LoadScene(SceneIndex.MENU);
			} else {
				_state = State.Starting;
				UpdateForState();
			}
		}
	}
}