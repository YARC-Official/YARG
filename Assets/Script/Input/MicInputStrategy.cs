using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Input {
	public sealed class MicInputStrategy : InputStrategy {
		private static readonly int SAMPLE_SCAN_SIZE;

		// Make this high enough to get the high notes
		private const int DOWNSCALED_SIZE = 196;

		private const int START_BOUND = 20;
		private const int WINDOW_SIZE = 48;
		private const float THRESHOLD = 0.1f;

		private float[] samples = new float[SAMPLE_SCAN_SIZE];
		private float[] optimizedSamples = new float[DOWNSCALED_SIZE];
		private float[] CMNDFvalues = new float[DOWNSCALED_SIZE - WINDOW_SIZE - START_BOUND];

		private float dbCache;
		private float? pitchCache;
		private (int, float) noteCache;

		public float timeSinceVoiceDetected = 0f;
		public bool VoiceDetected => dbCache >= 0f && timeSinceVoiceDetected >= 0.05f;

		public float VoiceFrequency => pitchCache ?? 0f;
		public int VoiceOctave => noteCache.Item1;
		public float VoiceNote => noteCache.Item2;

		static MicInputStrategy() {
			// Set the scan size relative to the sample rate
			SAMPLE_SCAN_SIZE = (int) (1024 * (float) AudioSettings.outputSampleRate / 48000);
		}

		public override string[] GetMappingNames() {
			return new string[0];
		}

		public override void UpdatePlayerMode() {
			if (microphoneIndex == -1) {
				return;
			}

			var watch = System.Diagnostics.Stopwatch.StartNew();

			var audioSource = MicPlayer.Instance.dummyAudioSources[this];

			// Get and optimize samples

			audioSource.GetOutputData(samples, 0);

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

			// Only update pitch if a voice is detected
			if (dbCache < 0f) {
				pitchCache = null;
				timeSinceVoiceDetected = 0f;
				return;
			} else {
				timeSinceVoiceDetected += Time.deltaTime;
			}

			// Pitch //

			for (int i = START_BOUND; i < DOWNSCALED_SIZE - WINDOW_SIZE; i++) {
				CMNDFvalues[i - START_BOUND] = CMNDF(0, i);
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
			float hertz = (float) DOWNSCALED_SIZE / SAMPLE_SCAN_SIZE * AudioSettings.outputSampleRate
				/ lowestCMNDF.Value;

			// Lerp
			if (pitchCache == null || timeSinceVoiceDetected < 0.05f) {
				pitchCache = hertz;
			} else {
				pitchCache = Mathf.Lerp(pitchCache ?? 0f, hertz, Time.deltaTime * 10f);
			}

			// Get the note number from the hertz value
			float midiNote = 12f * Mathf.Log((pitchCache ?? 0f) / 440f, 2f) + 69f;

			// Calculate the octave of the note
			int octave = (int) Mathf.Floor(midiNote / 12f);

			// Save to note cache
			noteCache = (octave, midiNote % 12f);

			watch.Stop();
			Debug.Log(watch.ElapsedMilliseconds + "ms");
		}

		/// <summary>
		/// Difference function.
		/// </summary>
		private float DF(int time, int lag) {
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
		private float CMNDF(int time, int lag) {
			if (lag == 0) {
				return 1;
			}

			float sum = 0f;
			for (int i = 1; i < lag; i++) {
				sum += DF(time, i + 1);
			}

			return DF(time, lag) / sum * lag;
		}

		public override void UpdateBotMode(List<NoteInfo> chart, float songTime) {
			// TODO
		}

		public override void UpdateNavigationMode() {
			// TODO
		}
	}
}