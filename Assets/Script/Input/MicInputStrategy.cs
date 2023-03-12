using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;
using YARG.Util;

namespace YARG.Input {
	public sealed class MicInputStrategy : InputStrategy {
		private static readonly int SAMPLE_SCAN_SIZE;

		private const float UPDATE_TIME = 1f / 20f;

		private const int TARGET_SIZE = 1024;
		private const int TARGET_SIZE_REF = 44800;

		private const int DOWNSCALED_SIZE = 512;

		private const int START_BOUND = 20;
		private const int WINDOW_SIZE = 64;
		private const float THRESHOLD = 0.05f;

		private float[] samples = new float[SAMPLE_SCAN_SIZE];

		private float updateTimer;

		private float dbCache;
		private float timeSinceVoiceDetected;

		private float pitchCache;
		private float lerpedPitch;

		private (float, int) noteCache;

		public bool VoiceDetected => dbCache >= 0f;

		public float TimeSinceNoVoice { get; private set; }

		public float VoiceNote => noteCache.Item1;
		public int VoiceOctave => noteCache.Item2;

		private LyricInfo botLyricInfo = null;

		static MicInputStrategy() {
			// Set the scan size relative to the sample rate
			SAMPLE_SCAN_SIZE = (int) (TARGET_SIZE * (float) AudioSettings.outputSampleRate / TARGET_SIZE_REF);
		}

		public override string[] GetMappingNames() {
			return new string[0];
		}

		public override void UpdatePlayerMode() {
			if (microphoneIndex == -1) {
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
				timeSinceVoiceDetected = 0f;

				TimeSinceNoVoice += Time.deltaTime;
				return;
			} else {
				TimeSinceNoVoice = 0f;
				timeSinceVoiceDetected += Time.deltaTime;
			}

			// Note //
			// Update LAST update's pitch.

			// Lerp
			if (timeSinceVoiceDetected < 0.07f) {
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

		public override void UpdateBotMode(object rawChart, float songTime) {
			var chart = (List<LyricInfo>) rawChart;

			// Get the next lyric
			while (chart.Count > botChartIndex && chart[botChartIndex].time <= songTime) {
				botLyricInfo = chart[botChartIndex];
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
				timeSinceVoiceDetected = 0f;
			} else {
				dbCache = 1f;
				timeSinceVoiceDetected += Time.deltaTime;
				TimeSinceNoVoice = 0f;

				float timeIntoNote = Play.Instance.SongTime - botLyricInfo.time;
				float rawNote = botLyricInfo.GetLerpedNoteAtTime(timeIntoNote);
				noteCache = Utils.SplitNoteToOctaveAndNote(rawNote);
			}

			// Constantly activate starpower
			CallStarpowerEvent();
		}

		public override void UpdateNavigationMode() {
			// TODO
		}
	}
}