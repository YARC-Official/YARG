using System;
using UnityEngine;

namespace YARG.Audio.PitchDetection {
	/// <summary>
	/// Tracks pitch
	/// </summary>
	public class PitchTracker {
		private struct FilterInfo {
			public IIRFilter High;
			public IIRFilter Low;

			public float[] Buffer;
			public CircularBuffer CircularBuffer;
		}

		// A1, Midi note 33, 55.0Hz
		private const float MIN_FREQUENCY = 50;
		// A#6. Midi note 92
		private const float MAX_FREQUENCY = 1600;

		private const float DETECT_OVERLAP_SEC = 0.005f;
		private const float MAX_OCTAVE_SEC_RATE = 10.0f;

		// Time offset between pitch averaging values
		private const float AVG_OFFSET = 0.005f;
		// Number of average pitch samples to take
		private const int AVG_COUNT = 1;
		// Amount of samples to store in the history Buffer
		private const float CIRCULAR_BUF_SAVE_TIME = 1.0f;

		// Default is 50ms, or one record every 20ms
		private const int PITCH_RECORDS_PER_SECOND = 50;

		private readonly PitchProcessor _dsp;

		private readonly int _pitchBufSize;

		private readonly int _detectOverlapSamples;
		private readonly float _maxOverlapDiff;
		private readonly int _samplesPerPitchBlock;

		private long _sampleReadPosition;

		private readonly FilterInfo[] _filters;

		public PitchTracker(float detectLevelThreshold = 0.01f, float sampleRate = 44100f) {
			_dsp = new PitchProcessor(sampleRate, MIN_FREQUENCY, MAX_FREQUENCY, detectLevelThreshold);

			_pitchBufSize = (int) ((1.0f / MIN_FREQUENCY * 2.0f + (AVG_COUNT - 1) * AVG_OFFSET) * sampleRate) + 16;
			_detectOverlapSamples = (int) (DETECT_OVERLAP_SEC * sampleRate);

			_maxOverlapDiff = MAX_OCTAVE_SEC_RATE * DETECT_OVERLAP_SEC;
			_samplesPerPitchBlock = (int) Mathf.Round(sampleRate / PITCH_RECORDS_PER_SECOND);

			// Create the high and low filters
			_filters = new FilterInfo[2];
			for (int i = 0; i < 2; i++) {
				float highFreq = i == 0 ? 280f : 1500f;

				var filter = new FilterInfo {
					Low = new IIRFilter(IIRFilterType.HP, 5, sampleRate) {
						FreqLow = 45
					},
					High = new IIRFilter(IIRFilterType.LP, 5, sampleRate) {
						FreqHigh = highFreq
					},
					Buffer = new float[_pitchBufSize + _detectOverlapSamples],
					CircularBuffer = new CircularBuffer((int) (CIRCULAR_BUF_SAVE_TIME * sampleRate + 0.5f) + 10000)
				};

				_filters[i] = filter;
			}
		}

		/// <summary>
		/// Reset the pitch tracker. Call this when the sample position is
		/// not consecutive from the previous position
		/// </summary>
		public void Reset() {
			_sampleReadPosition = 0;

			foreach (var filter in _filters) {
				filter.High.Reset();
				filter.Low.Reset();
				Array.Clear(filter.Buffer, 0, filter.Buffer.Length);

				filter.CircularBuffer.Reset();
				filter.CircularBuffer.Clear();
				filter.CircularBuffer.StartPosition = -_detectOverlapSamples;
				filter.CircularBuffer.Available = _detectOverlapSamples;
			}
		}

		/// <summary>
		/// Process the passed in Buffer of data. During this call, the PitchDetected event will
		/// be fired zero or more times, depending how many pitch records will fit in the new
		/// and previously cached Buffer.
		///
		/// This means that there is no size restriction on the Buffer that is passed into ProcessBuffer.
		/// For instance, ProcessBuffer can be called with one very large Buffer that contains all of the
		/// audio to be processed (many PitchDetected events will be fired), or just a small Buffer at
		/// a time which is more typical for realtime applications. In the latter case, the PitchDetected
		/// event might not be fired at all since additional calls must first be made to accumulate enough
		/// data do another pitch detect operation.
		/// </summary>
		/// <param name="input">Input Buffer. Samples must be in the range -1.0 to 1.0</param>
		public float? ProcessBuffer(ReadOnlySpan<float> input) {
			if (input == null) {
				throw new ArgumentNullException(nameof(input), "Input buffer cannot be null");
			}

			float? detectedPitch = null;

			int samplesProcessed = 0;
			while (samplesProcessed < input.Length) {
				var frameCount = Mathf.Min(input.Length - samplesProcessed, _pitchBufSize + _detectOverlapSamples);

				foreach (var filter in _filters) {
					filter.Low.FilterBuffer(input, samplesProcessed, filter.Buffer, 0, frameCount);
					filter.High.FilterBuffer(filter.Buffer, 0, filter.Buffer, 0, frameCount);
					filter.CircularBuffer.Write(filter.Buffer, frameCount);
				}

				// Loop while there is enough samples in the circular Buffer
				while (_filters[0].CircularBuffer.Read(_filters[0].Buffer, _sampleReadPosition, _pitchBufSize + _detectOverlapSamples)) {
					_filters[1].CircularBuffer.Read(_filters[1].Buffer, _sampleReadPosition, _pitchBufSize + _detectOverlapSamples);
					_sampleReadPosition += _samplesPerPitchBlock;

					var pitch1 = _dsp.DetectPitch(_filters[0].Buffer, _filters[1].Buffer, _pitchBufSize);

					if (pitch1 <= 0f) {
						continue;
					}

					// Shift the buffers left by the overlapping amount
					foreach (var filter in _filters) {
						SafeCopy(filter.Buffer, filter.Buffer, _detectOverlapSamples, 0, _pitchBufSize);
					}

					var pitch2 = _dsp.DetectPitch(_filters[0].Buffer, _filters[1].Buffer, _pitchBufSize);

					if (pitch2 <= 0f) {
						continue;
					}

					var fDiff = Mathf.Max(pitch1, pitch2) / Mathf.Min(pitch1, pitch2) - 1;

					if (fDiff < _maxOverlapDiff) {
						detectedPitch = (pitch1 + pitch2) * 0.5f;
					}
				}

				samplesProcessed += frameCount;
			}

			return detectedPitch;
		}

		/// <summary>
		/// Copy the values from one Buffer to a different or the same Buffer.
		/// It is safe to copy to the same Buffer, even if the areas overlap
		/// </summary>
		private static void SafeCopy<T>(T[] from, T[] to, int fromStart, int toStart, int length) {
			if (to == null || from.Length == 0 || to.Length == 0)
				return;

			var fromEndIdx = fromStart + length;
			var toEndIdx = toStart + length;

			if (fromStart < 0) {
				toStart -= fromStart;
				fromStart = 0;
			}

			if (toStart < 0) {
				fromStart -= toStart;
				toStart = 0;
			}

			if (fromEndIdx >= from.Length) {
				toEndIdx -= fromEndIdx - from.Length + 1;
				fromEndIdx = from.Length - 1;
			}

			if (toEndIdx >= to.Length) {
				fromEndIdx -= toEndIdx - to.Length + 1;
				toEndIdx = from.Length - 1;
			}

			if (fromStart < toStart) {
				// Shift right, so start at the right
				for (int fromIdx = fromEndIdx, toIdx = toEndIdx; fromIdx >= fromStart; fromIdx--, toIdx--)
					to[toIdx] = from[fromIdx];
			} else {
				// Shift left, so start at the left
				for (int fromIdx = fromStart, toIdx = toStart; fromIdx <= fromEndIdx; fromIdx++, toIdx++)
					to[toIdx] = from[fromIdx];
			}
		}
	}
}