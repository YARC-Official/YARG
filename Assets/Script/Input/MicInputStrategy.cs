using System.Collections.Generic;
using UnityEngine;
using YARG.Data;
using YARG.PlayMode;

namespace YARG.Input {
	public class MicInputStrategy : InputStrategy {
		private const int SAMPLE_SCAN_SIZE = 1024;

		private const int PITCH_COMBINE_THRESHOLD = 48;

		// private const float PITCH_THRESHOLD = 0.02f;

		private float[] samples = new float[SAMPLE_SCAN_SIZE];
		// private float[] spectrum = new float[SAMPLE_SCAN_SIZE];

		private float dbCache;
		private float? pitchCache;
		private (int, float) noteCache;

		public bool VoiceDetected => dbCache >= 0f;

		public float VoiceFrequency => pitchCache ?? 0f;
		public int VoiceOctave => noteCache.Item1;
		public float VoiceNote => noteCache.Item2;

		public override string[] GetMappingNames() {
			return new string[0];
		}

		public override void UpdatePlayerMode() {
			if (microphoneIndex == -1) {
				return;
			}

			var audioSource = MicPlayer.Instance.dummyAudioSources[this];

			// Decibels

			audioSource.GetOutputData(samples, 0);

			// Get the root mean square
			float sum = 0f;
			for (int i = 0; i < SAMPLE_SCAN_SIZE; i++) {
				sum += samples[i] * samples[i];
			}
			sum = Mathf.Sqrt(sum / SAMPLE_SCAN_SIZE);

			// Convert that to decibels
			dbCache = 20f * Mathf.Log10(sum * 180f);
			if (dbCache < -160f) {
				dbCache = -160f;
			}

			// Only update pitch if a voice is detected
			if (!VoiceDetected) {
				pitchCache = null;
				return;
			}

			// Pitch

			// Get all zero indexes
			List<float> zeros = new();
			for (int i = 1; i < samples.Length; i++) {
				float ay = samples[i - 1];
				float by = samples[i];

				float ax = i - 1;
				float bx = i;

				// Check if the two points are on opposite sides of the x-axis
				if ((ay > 0 && by < 0) || (ay < 0 && by > 0)) {
					// Calculate the slope of the line between the two points
					float slope = (by - ay) / (bx - ax);

					// Get the x-intercept
					zeros.Add(ax - (ay / slope));
				}
			}

			// Skip if not enough zeros
			if (zeros.Count < 2) {
				return;
			}

			// Get the distance between each zero to calculate frequency
			float distanceSum = 0f;
			int num = 0;
			for (int i = 1; i < zeros.Count; i++) {
				float last = zeros[i - 1];
				float current = zeros[i];

				if (current - last <= PITCH_COMBINE_THRESHOLD) {
					continue;
				}

				distanceSum += current - last;
				num++;
			}

			if (num <= 0) {
				return;
			}

			// Convert to hertz
			float avgCyclesPerScan = SAMPLE_SCAN_SIZE / (distanceSum / num) / 2f;
			float scansInSecond = AudioSettings.outputSampleRate / SAMPLE_SCAN_SIZE;
			float hertz = avgCyclesPerScan * scansInSecond;

			// Lerp
			if (pitchCache == null) {
				pitchCache = hertz;
			} else {
				pitchCache = Mathf.Lerp(pitchCache ?? 0f, hertz, Time.deltaTime * 6f);
			}

			// Get the note number from the hertz value
			float midiNote = 12f * Mathf.Log((pitchCache ?? 0f) / 440f, 2f) + 69f;

			// Calculate the octave of the note
			int octave = (int) Mathf.Floor(midiNote / 12f) - 1;

			// Save to note cache
			noteCache = (octave, midiNote % 12f);
		}

		public override void UpdateBotMode(List<NoteInfo> chart, float songTime) {
			// TODO
		}

		public override void UpdateNavigationMode() {
			// TODO
		}
	}
}