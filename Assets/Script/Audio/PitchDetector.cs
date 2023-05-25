using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace YARG.Audio {
	public static class PitchDetector {
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
			for (int i = 0; i < length; i += SKIP_AMOUNT, count++) {
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

		public static unsafe float? GetPitch(IntPtr buffer, int bufferByteLength) {
			var bufferPtr = (float*) buffer;
			int realLength = bufferByteLength / sizeof(float);
			int length = realLength / SKIP_AMOUNT;

			// Lower the quality of the stream
			var optimizedSamples = stackalloc float[length];
			for (int i = 0; i < length; i++) {
				optimizedSamples[i] = bufferPtr![i * SKIP_AMOUNT];
			}

			// Pass through the cumulative mean normalized difference function
			var CMNDFvalues = stackalloc float[length - WINDOW_SIZE - START_BOUND];
			for (int i = START_BOUND; i < length - WINDOW_SIZE; i++) {
				CMNDFvalues[i - START_BOUND] = CMNDF(optimizedSamples, 0, i);
			}

			// Get the lowest CMNDF value
			float? lowestCMNDF = null;
			for (int i = 0; i < length; i++) {
				if (CMNDFvalues[i] < THRESHOLD) {
					lowestCMNDF = i + START_BOUND;
					break;
				}
			}

			// If nothing met the threshold, just get the minimum
			if (lowestCMNDF == null) {
				lowestCMNDF = CMNDFvalues[0];
				for (int i = 1; i < length; i++) {
					var val = CMNDFvalues[i];
					if (val < lowestCMNDF) {
						lowestCMNDF = val;
					}
				}
				lowestCMNDF += START_BOUND;
			}

			// Convert to Hz
			float hertz = (float) length / realLength * 44100 / lowestCMNDF.Value;

			// If the hertz is over 800, it is probably just a fricative (th, f, s, etc.)
			if (hertz > 800f) {
				return null;
			}

			return hertz;
		}

		/// <summary>
		/// Difference function.
		/// </summary>
		private static unsafe float DF(float* optimizedSamples, int time, int lag) {
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
		private static unsafe float CMNDF(float* optimizedSamples, int time, int lag) {
			if (lag == 0) {
				return 1;
			}

			float sum = 0f;
			for (int i = 1; i < lag; i++) {
				sum += DF(optimizedSamples, time, i + 1);
			}

			return DF(optimizedSamples, time, lag) / sum * lag;
		}
	}
}