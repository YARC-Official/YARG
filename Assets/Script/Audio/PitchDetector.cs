using System;
using UnityEngine;

namespace YARG.Audio {
	public static class PitchDetector {
		private const int TARGET_SIZE = 1024;
		private const int TARGET_SIZE_REF = 44100;

		private const int SKIP_AMOUNT = 4;

		private const int START_BOUND = 20;
		private const int WINDOW_SIZE = 64;
		private const float THRESHOLD = 0.05f;

		public static unsafe float GetAmplitude(IntPtr buffer, int bufferByteLength) {
			var bufferPtr = (float*) buffer;
			int length = bufferByteLength / sizeof(float);

			// Get the root mean square
			float sum = 0f;
			int count = 0;
			for (int i = 0; i < length; i += 4, count++) {
				sum += bufferPtr![i] * bufferPtr![i];
			}

			sum = Mathf.Sqrt(sum / count);

			// Convert to decibels
			float decibels = 20f * Mathf.Log10(sum * 180f);
			if (decibels < -160f) {
				return -160f;
			}

			return decibels;
		}
	}
}