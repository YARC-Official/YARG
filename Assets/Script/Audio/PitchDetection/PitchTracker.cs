using System;
using UnityEngine;

namespace YARG.Audio.PitchDetection {
	/// <summary>
	/// Tracks pitch
	/// </summary>
	public class PitchTracker {
		private const int OCTAVE_STEPS = 96;

		// A1, Midi note 33, 55.0Hz
		private const float MINIMUM_DETECTED_FREQUENCY = 50;
		// A#6. Midi note 92
		private const float MAXIMUM_DETECTED_FREQUENCY = 1600;

		private const float DETECT_OVERLAP_SEC = 0.005f;
		private const float MAX_OCTAVE_SEC_RATE = 10.0f;

		// time offset between pitch averaging values
		private const float AVG_OFFSET = 0.005f;
		// number of average pitch samples to take
		private const int AVG_COUNT = 1;
		// Amount of samples to store in the history Buffer
		private const float CIRCULAR_BUF_SAVE_TIME = 1.0f;

		private PitchProcessor _DSP;
		private CircularBuffer _circularBufferLow;
		private CircularBuffer _circularBufferHigh;
		private readonly float _sampleRate;

		// -40dB
		private float _detectLevelThreshold = 0.01f;
		// default is 50, or one record every 20ms
		private int _pitchRecordsPerSecond = 50;

		private float[] _pitchBufLow, _pitchBufHigh;
		private int _pitchBufSize;

		private int _detectOverlapSamples;
		private float _maxOverlapDiff;

		private IIRFilter _IIRFilterLowLow, _IIRFilterLowHigh, _IIRFilterHighLow, _IIRFilterHighHigh;

		public PitchTracker(float sampleRate = 44100f) {
			_sampleRate = sampleRate;
			Setup();
		}

		/// <summary>
		/// Set the detect level threshold, The value must be between 0.0001f and 1.0f (-80 dB to 0 dB)
		/// </summary>
		public float DetectLevelThreshold {
			set {
				var newValue = Mathf.Max(0.0001f, Mathf.Min(1.0f, value));
				if (_detectLevelThreshold == newValue) {
					return;
				}

				_detectLevelThreshold = newValue;
				Setup();
			}
		}

		/// <summary>
		/// Return the samples per pitch block
		/// </summary>
		public int SamplesPerPitchBlock { get; private set; }

		/// <summary>
		/// Get or set the number of pitch records per second (default is 50, or one record every 20ms)
		/// </summary>
		public int PitchRecordsPerSecond {
			get => _pitchRecordsPerSecond;
			set {
				_pitchRecordsPerSecond = Mathf.Max(1, Mathf.Min(100, value));
				Setup();
			}
		}

		/// <summary>
		/// Get the frequency step
		/// </summary>
		public static float FrequencyStep => Mathf.Pow(2f, 1f / OCTAVE_STEPS);

		/// <summary>
		/// Get the number of samples that the detected pitch is offset from the Input Buffer.
		/// This is just an estimate to sync up the samples and detected pitch
		/// </summary>
		public int DetectSampleOffset => (_pitchBufSize + _detectOverlapSamples) / 2;

		/// <summary>
		/// Get the current pitch position
		/// </summary>
		public long SampleReadPosition { get; private set; }

		/// <summary>
		/// Reset the pitch tracker. Call this when the sample position is
		/// not consecutive from the previous position
		/// </summary>
		public void Reset() {
			SampleReadPosition = 0;

			_IIRFilterLowLow.Reset();
			_IIRFilterLowHigh.Reset();
			_IIRFilterHighLow.Reset();
			_IIRFilterHighHigh.Reset();

			_circularBufferLow.Reset();
			_circularBufferLow.Clear();
			_circularBufferHigh.Reset();
			_circularBufferHigh.Clear();
			Array.Clear(_pitchBufLow, 0, _pitchBufLow.Length);
			Array.Clear(_pitchBufHigh, 0, _pitchBufHigh.Length);

			_circularBufferLow.StartPosition = -_detectOverlapSamples;
			_circularBufferLow.Available = _detectOverlapSamples;
			_circularBufferHigh.StartPosition = -_detectOverlapSamples;
			_circularBufferHigh.Available = _detectOverlapSamples;
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

				_IIRFilterLowLow.FilterBuffer(input, samplesProcessed, _pitchBufLow, 0, frameCount);
				_IIRFilterLowHigh.FilterBuffer(_pitchBufLow, 0, _pitchBufLow, 0, frameCount);

				_IIRFilterHighLow.FilterBuffer(input, samplesProcessed, _pitchBufHigh, 0, frameCount);
				_IIRFilterHighHigh.FilterBuffer(_pitchBufHigh, 0, _pitchBufHigh, 0, frameCount);

				_circularBufferLow.Write(_pitchBufLow, frameCount);
				_circularBufferHigh.Write(_pitchBufHigh, frameCount);

				// Loop while there is enough samples in the circular Buffer
				while (_circularBufferLow.Read(_pitchBufLow, SampleReadPosition, _pitchBufSize + _detectOverlapSamples)) {
					_circularBufferHigh.Read(_pitchBufHigh, SampleReadPosition, _pitchBufSize + _detectOverlapSamples);
					SampleReadPosition += SamplesPerPitchBlock;

					var pitch1 = _DSP.DetectPitch(_pitchBufLow, _pitchBufHigh, _pitchBufSize);

					if (pitch1 <= 0f) {
						continue;
					}

					// Shift the buffers left by the overlapping amount
					SafeCopy(_pitchBufLow, _pitchBufLow, _detectOverlapSamples, 0, _pitchBufSize);
					SafeCopy(_pitchBufHigh, _pitchBufHigh, _detectOverlapSamples, 0, _pitchBufSize);

					var pitch2 = _DSP.DetectPitch(_pitchBufLow, _pitchBufHigh, _pitchBufSize);

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

		/// <summary>
		/// Setup
		/// </summary>
		private void Setup() {
			if (_sampleRate < 1)
				return;

			_DSP = new PitchProcessor(_sampleRate, MINIMUM_DETECTED_FREQUENCY, MAXIMUM_DETECTED_FREQUENCY,
				_detectLevelThreshold);

			_IIRFilterLowLow = new IIRFilter(IIRFilterType.HP, 5, (float) _sampleRate) { FreqLow = 45 };

			_IIRFilterLowHigh = new IIRFilter(IIRFilterType.LP, 5, (float) _sampleRate) { FreqHigh = 280 };

			_IIRFilterHighLow = new IIRFilter(IIRFilterType.HP, 5, (float) _sampleRate) { FreqLow = 45 };

			_IIRFilterHighHigh = new IIRFilter(IIRFilterType.LP, 5, (float) _sampleRate) { FreqHigh = 1500 };

			_detectOverlapSamples = (int) (DETECT_OVERLAP_SEC * _sampleRate);
			_maxOverlapDiff = MAX_OCTAVE_SEC_RATE * DETECT_OVERLAP_SEC;

			_pitchBufSize =
				(int) ((1.0f / MINIMUM_DETECTED_FREQUENCY * 2.0f + (AVG_COUNT - 1) * AVG_OFFSET) * _sampleRate) + 16;
			_pitchBufLow = new float[_pitchBufSize + _detectOverlapSamples];
			_pitchBufHigh = new float[_pitchBufSize + _detectOverlapSamples];
			SamplesPerPitchBlock = (int) Mathf.Round(_sampleRate / _pitchRecordsPerSecond);

			_circularBufferLow = new CircularBuffer((int) (CIRCULAR_BUF_SAVE_TIME * _sampleRate + 0.5f) + 10000);
			_circularBufferHigh = new CircularBuffer((int) (CIRCULAR_BUF_SAVE_TIME * _sampleRate + 0.5f) + 10000);
		}
	}
}