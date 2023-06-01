using System;
using System.Linq;
using UnityEngine;

namespace YARG.Audio {
	public class PitchDetector {
		private const int SAMPLE_RATE = 44100;
		private const int BUFFER_SIZE = 3500;

		private const int AMP_SKIP_AMOUNT = 4;

		private readonly float[] _buffer = new float[BUFFER_SIZE];
		private readonly float[] _spectrum = new float[BUFFER_SIZE];

		public float GetAmplitude(ReadOnlySpan<float> buffer) {
			// Get the root mean square
			float sum = 0f;
			int count = 0;
			for (int i = 0; i < BUFFER_SIZE; i += AMP_SKIP_AMOUNT, count++) {
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
			// var sw = new System.Diagnostics.Stopwatch();
			// sw.Start();

			for (int i = 0; i < BUFFER_SIZE; i++) {
				_buffer[i] = buffer[i];
			}

			// Auto-correlate it (and copy the data into the buffer at the same time)
			// Array.Clear(_buffer, 0, BUFFER_SIZE);
			// for (int lag = 0; lag < BUFFER_SIZE; lag++) {
			// 	for (int i = 0; i < BUFFER_SIZE - lag; i++) {
			// 		_buffer[i] += buffer[i] + buffer[i + lag];
			// 	}
			// }
			//
			// // Apply a Hamming window to prevent spectral leakage
			// for (int i = 0; i < BUFFER_SIZE; i++) {
			// 	_buffer[i] *= _window[i];
			// }

			// Perform the FFT
			FFT.Do(_buffer, _spectrum);

			// Find the pitch with the highest magnitude
			int maxIndex = 0;
			float maxMagnitude = 0f;
			for (int i = 0; i < BUFFER_SIZE / 2; i++) {
				float magnitude = _spectrum[i];
				if (magnitude > maxMagnitude) {
					maxMagnitude = magnitude;
					maxIndex = i;
				}
			}

			// sw.Stop();
			// Debug.Log(sw.ElapsedMilliseconds + "ms");

			return (float) maxIndex * SAMPLE_RATE / BUFFER_SIZE;
		}
	}
}