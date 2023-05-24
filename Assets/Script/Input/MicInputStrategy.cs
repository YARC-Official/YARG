using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Input {
	public sealed class MicInputStrategy : InputStrategy {
		public const string CONFIRM = "confirm";
		public const string BACK = "back";
		public const string MENU_ACTION_1 = "menu_action_1";
		public const string MENU_ACTION_2 = "menu_action_2";
		public const string MENU_ACTION_3 = "menu_action_3";

		public const string PAUSE = "pause";
		public const string UP = "up";
		public const string DOWN = "down";

		private static readonly int SAMPLE_SCAN_SIZE;

		private const float UPDATE_TIME = 1f / 20f;

		private const int TARGET_SIZE = 1024;
		private const int TARGET_SIZE_REF = 44800;

		private const int DOWNSCALED_SIZE = 512;

		private const int START_BOUND = 20;
		private const int WINDOW_SIZE = 64;
		private const float THRESHOLD = 0.05f;

		private List<LyricInfo> botChart;

		private float[] samples = new float[SAMPLE_SCAN_SIZE];

		private float updateTimer;

		private float dbCache;

		private float pitchCache;
		private float lerpedPitch;

		private (float, int) noteCache;

		public bool VoiceDetected => dbCache >= 0f;

		public float TimeSinceNoVoice { get; private set; }
		public float TimeSinceVoiceDetected { get; private set; }

		public float VoiceNote => noteCache.Item1;
		public int VoiceOctave => noteCache.Item2;

		private LyricInfo botLyricInfo = null;

		static MicInputStrategy() {
			// Set the scan size relative to the sample rate
			SAMPLE_SCAN_SIZE = (int) (TARGET_SIZE * (float) AudioSettings.outputSampleRate / TARGET_SIZE_REF);
		}

		protected override Dictionary<string, ControlBinding> GetMappings() => new() {
			{ CONFIRM,       new(BindingType.BUTTON, "Confirm/Select (Green)", CONFIRM) },
			{ BACK,          new(BindingType.BUTTON, "Back (Red)", BACK) },
			{ MENU_ACTION_1, new(BindingType.BUTTON, "Menu Action 1 (Yellow)", MENU_ACTION_1) },
			{ MENU_ACTION_2, new(BindingType.BUTTON, "Menu Action 2 (Blue)", MENU_ACTION_2) },
			{ MENU_ACTION_3, new(BindingType.BUTTON, "Menu Action 3 (Orange)", MENU_ACTION_3) },

			{ PAUSE,         new(BindingType.BUTTON, "Pause", PAUSE) },
			{ UP,            new(BindingType.BUTTON, "Navigate Up", UP) },
			{ DOWN,          new(BindingType.BUTTON, "Navigate Down", DOWN) },
		};

		public override string GetIconName() {
			return "vocals";
		}

		protected override void UpdatePlayerMode() { }

		protected override void OnUpdate() {
			base.OnUpdate();

			if (microphoneIndex == INVALID_MIC_INDEX) {
				return;
			}

			// Not in a song yet
			if (MicPlayer.Instance == null) {
				return;
			}

			var audioSource = MicPlayer.Instance.dummyAudioSources[this];

			// Get and optimize samples
			audioSource.GetOutputData(samples, 0);
			float[] optimizedSamples = new float[DOWNSCALED_SIZE];
			int skip = SAMPLE_SCAN_SIZE / DOWNSCALED_SIZE;
			for (int i = 0; i < DOWNSCALED_SIZE; i++) {
				optimizedSamples[i] = samples[i * skip];
			}

			// Decibels //

			// Get the root mean square
			float sum = 0f;
			for (int i = 0; i < DOWNSCALED_SIZE; i++) {
				sum += optimizedSamples[i] * optimizedSamples[i];
			}
			sum = Mathf.Sqrt(sum / DOWNSCALED_SIZE);

			// Convert that to decibels
			dbCache = 20f * Mathf.Log10(sum * 180f);
			if (dbCache < -160f) {
				dbCache = -160f;
			}

			// Count down the update timer (for below)
			updateTimer -= Time.deltaTime;

			// Only update pitch if a voice is detected
			if (dbCache < 0f) {
				TimeSinceVoiceDetected = 0f;

				TimeSinceNoVoice += Time.deltaTime;
				return;
			} else {
				TimeSinceNoVoice = 0f;
				TimeSinceVoiceDetected += Time.deltaTime;
			}

			// Note //
			// Update LAST update's pitch.

			// Lerp
			if (TimeSinceVoiceDetected < 0.07f) {
				lerpedPitch = pitchCache;
			} else {
				lerpedPitch = Mathf.Lerp(lerpedPitch, pitchCache, Time.deltaTime * 10f);
			}

			// Get the note number from the hertz value
			float midiNote = 0f;
			if (lerpedPitch != 0f) {
				midiNote = 12f * Mathf.Log(lerpedPitch / 440f, 2f) + 69f;
			}

			// Calculate the octave of the note
			int octave = (int) Mathf.Floor(midiNote / 12f);

			// Save to note cache
			noteCache = (midiNote % 12f, octave);

			// Pitch //

			// Buffer pitch detection a bit to prevent lag
			if (updateTimer > 0f) {
				return;
			}
			updateTimer = UPDATE_TIME;

			// Do this on a different thread to prevent blocking
			int sampleRate = AudioSettings.outputSampleRate;
			new Thread(_ => {
				float[] CMNDFvalues = new float[DOWNSCALED_SIZE - WINDOW_SIZE - START_BOUND];
				for (int i = START_BOUND; i < DOWNSCALED_SIZE - WINDOW_SIZE; i++) {
					CMNDFvalues[i - START_BOUND] = CMNDF(optimizedSamples, 0, i);
				}

				float? lowestCMNDF = null;
				for (int i = 0; i < CMNDFvalues.Length; i++) {
					float val = CMNDFvalues[i];
					if (val < THRESHOLD) {
						lowestCMNDF = i + START_BOUND;
						break;
					}
				}
				lowestCMNDF ??= CMNDFvalues.Min() + START_BOUND;

				// Convert to hertz (and calculate the sample rate)
				// TODO: Something is not 100% right here. As long as you don't change the constants, it is fine.
				float hertz = (float) DOWNSCALED_SIZE / SAMPLE_SCAN_SIZE * sampleRate / lowestCMNDF.Value;

				// If the hertz is over 800, it is probably just a fricative (th, f, s, etc.)
				if (hertz > 800f) {
					return;
				}

				// Save. VoiceFrequency will be updated in the "Note" section.
				pitchCache = hertz;
			}).Start();

			// Activate starpower if loud!
			if (dbCache > 8f && TimeSinceVoiceDetected < 0.5f) {
				CallStarpowerEvent();
			}
		}

		/// <summary>
		/// Difference function.
		/// </summary>
		private float DF(float[] optimizedSamples, int time, int lag) {
			// The following is an optimized version of these two methods.
			// These are here for clarity.

			// private float ACF(int time, int lag) {
			// 	float result = 0f;
			// 	for (int i = 0; i < WINDOW_SIZE; i++) {
			// 		result += optimizedSamples[time + i] * optimizedSamples[time + i + lag];
			// 	}
			// 	return result;
			// }

			// private float DF(int time, int lag) {
			// 	return ACF(WINDOW_SIZE, time, 0)
			// 		+ ACF(WINDOW_SIZE, time + lag, 0)
			// 		- (2f * ACF(WINDOW_SIZE, time, lag));
			// }

			float result = 0f;
			int index = time + lag;
			for (int i = 0; i < WINDOW_SIZE; i++) {
				float sample1 = optimizedSamples[time + i];
				float sample2 = optimizedSamples[index + i];
				result += (sample1 * sample1) + (sample2 * sample2) - (2f * sample1 * sample2);
			}
			return result;
		}

		/// <summary>
		/// Cumulative mean normalized difference function.
		/// </summary>
		private float CMNDF(float[] optimizedSamples, int time, int lag) {
			if (lag == 0) {
				return 1;
			}

			float sum = 0f;
			for (int i = 1; i < lag; i++) {
				sum += DF(optimizedSamples, time, i + 1);
			}

			return DF(optimizedSamples, time, lag) / sum * lag;
		}

		public override void InitializeBotMode(object rawChart) {
			botChart = (List<LyricInfo>) rawChart;
		}

		protected override void UpdateBotMode() {
			if (botChart == null) {
				return;
			}

			float songTime = Play.Instance.SongTime;

			// Get the next lyric
			while (botChart.Count > botChartIndex && botChart[botChartIndex].time <= songTime) {
				botLyricInfo = botChart[botChartIndex];
				botChartIndex++;
			}

			// If we are past the lyric, null
			if (botLyricInfo?.EndTime < songTime) {
				botLyricInfo = null;
			}

			// Set info based on lyric
			if (botLyricInfo == null) {
				dbCache = -1f;
				TimeSinceNoVoice += Time.deltaTime;
				TimeSinceVoiceDetected = 0f;
			} else {
				dbCache = 1f;
				TimeSinceVoiceDetected += Time.deltaTime;
				TimeSinceNoVoice = 0f;

				float timeIntoNote = Play.Instance.SongTime - botLyricInfo.time;
				noteCache = botLyricInfo.GetLerpedAndSplitNoteAtTime(timeIntoNote);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		protected override void UpdateNavigationMode() {
			NavigationEventForMapping(MenuAction.Confirm, CONFIRM);
			NavigationEventForMapping(MenuAction.Back, BACK);

			NavigationEventForMapping(MenuAction.Shortcut1, MENU_ACTION_1);
			NavigationEventForMapping(MenuAction.Shortcut2, MENU_ACTION_2);
			NavigationEventForMapping(MenuAction.Shortcut3, MENU_ACTION_3);

			NavigationEventForMapping(MenuAction.Up, UP);
			NavigationEventForMapping(MenuAction.Down, DOWN);

			if (WasMappingPressed(PAUSE)) {
				CallPauseEvent();
			}
		}

		public override Instrument[] GetAllowedInstruments() {
			return new Instrument[] {
				Instrument.VOCALS,
				Instrument.HARMONY,
			};
		}

		public override string GetTrackPath() {
			return null;
		}

		public override void ResetForSong() {
			base.ResetForSong();

			updateTimer = default;

			dbCache = default;

			pitchCache = default;
			lerpedPitch = default;

			noteCache = default;

			TimeSinceNoVoice = 0f;
			TimeSinceVoiceDetected = 0f;

			botLyricInfo = null;
		}
	}
}