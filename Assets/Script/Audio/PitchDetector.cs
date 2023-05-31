using System;
using UnityEngine;

namespace YARG.Audio {
	public class PitchDetector {
		private const int SAMPLE_RATE = 44100;
		private const int MAX_BUFFER_SIZE = 1024;

		private const int AMP_SKIP_AMOUNT = 4;

		private readonly float[] _buffer = new float[MAX_BUFFER_SIZE];
		private readonly float[] _spectrum = new float[MAX_BUFFER_SIZE];

		public float GetAmplitude(ReadOnlySpan<float> buffer) {
			// Get the root mean square
			float sum = 0f;
			int count = 0;
			for (int i = 0; i < MAX_BUFFER_SIZE; i += AMP_SKIP_AMOUNT, count++) {
				sum += buffer[i] * buffer[i];
			}
			sum = Mathf.Sqrt(sum / count);

			// Convert to decibels
			float decibels = 20f * Mathf.Log10(sum * 180f);
			if (decibels < -160f) {
				return -160f;
			}

			return decibels;
		}

		public float GetPitch(ReadOnlySpan<float> buffer) {
			// Copy the sound data into the buffer
			for (int i = 0; i < MAX_BUFFER_SIZE; i++) {
				_buffer[i] = buffer[i];
			}

			// Perform the FFT
			FFT.Do(_buffer, _spectrum);

			// Find the pitch with the highest magnitude
			float pitch = 0f;
			float maxMagnitude = 0f;

			for (int i = 0; i < MAX_BUFFER_SIZE / 2; i++) {
				float magnitude = _spectrum[i];

				if (magnitude > maxMagnitude) {
					maxMagnitude = magnitude;
					pitch = (float) SAMPLE_RATE / i;
				}
			}

			return pitch;
		}
	}
}