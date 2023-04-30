using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using YARG.Input;
using YARG.Settings;

namespace YARG {
	public class Calibrator : MonoBehaviour {
		private const float NOTE_SPACING = 0.75f;
		private const float ERROR = 0.05f;

		private enum State {
			LOOKING_FOR_PLAYER,
			CALIBRATING,
			DONE
		}

		[SerializeField]
		private AudioSource musicPlayer = null;
		[SerializeField]
		private TextMeshProUGUI text = null;
		[SerializeField]
		private TextMeshProUGUI subtext = null;

		private State state = State.LOOKING_FOR_PLAYER;

		private InputStrategy player = null;
		private List<float> hitTimes = new();

		private void Start() {
			foreach (var player in PlayerManager.players) {
				player.inputStrategy.GenericCalibrationEvent += OnGenericCalibration;
			}
			text.text = "Strum on any device. That device will calibrate.";
		}

		private void Update() {
			// End calibration when the music is done
			if (state == State.CALIBRATING) {
				if (!musicPlayer.isPlaying) {
					state = State.DONE;
					SetCalibration();
				}
			}
		}

		private void SetCalibration() {
			float average = -hitTimes.Average() + ERROR;
			SettingsManager.Settings.CalibrationNumber.Data = (int) (average * 1000f);

			text.text = $"Calibration set to: {average}.";
			subtext.text = "Strum to continue...";
		}

		private void OnGenericCalibration(InputStrategy inputStrategy) {
			if (state == State.LOOKING_FOR_PLAYER) {
				text.text = "Strum on each click.";
				musicPlayer.Play();

				player = inputStrategy;
				state = State.CALIBRATING;
			} else if (state == State.CALIBRATING) {
				// Skip if wrong player
				if (player != inputStrategy) {
					return;
				}

				// Skip start
				if (musicPlayer.time < NOTE_SPACING * 3f) {
					return;
				}

				// Calculate distance to click
				float dist = (musicPlayer.time - NOTE_SPACING / 2f) % NOTE_SPACING - NOTE_SPACING / 2f;
				subtext.text = dist.ToString();

				// Add to hit times
				hitTimes.Add(dist);
			} else {
				// Unbind events
				foreach (var player in PlayerManager.players) {
					player.inputStrategy.GenericCalibrationEvent -= OnGenericCalibration;
				}

				GameManager.Instance.LoadScene(SceneIndex.MENU);
			}
		}
	}
}