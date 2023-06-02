using System;
using UnityEngine;

namespace YARG.Audio.PitchDetection {
	internal class CircularBuffer {
		private readonly int _size;
		private int _startBufferOffset;
		private int _availableBuffer;
		private readonly float[] _buffer;

		public long StartPosition { get; set; }

		public int Available {
			set => _availableBuffer = Mathf.Min(value, _size);
		}

		public CircularBuffer(int size) {
			_size = size;

			if (_size > 0) {
				_buffer = new float[_size];
			} else {
				throw new InvalidOperationException("Invalid buffer size");
			}
		}

		/// <summary>
		/// Reset to the beginning of the Buffer
		/// </summary>
		public void Reset() {
			StartPosition = 0;
			_startBufferOffset = 0;
			_availableBuffer = 0;
		}

		/// <summary>
		/// Clear the Buffer
		/// </summary>
		public void Clear() {
			Array.Clear(_buffer, 0, _buffer.Length);
		}

		/// <summary>
		/// Write data into the Buffer
		/// </summary>
		public int Write(ReadOnlySpan<float> inputBuffer, int count) {
			count = Mathf.Min(count, _size);

			var startPos = _availableBuffer != _size ? _availableBuffer : _startBufferOffset;
			var pass1Count = Mathf.Min(count, _size - startPos);
			var pass2Count = count - pass1Count;

			inputBuffer[..pass1Count].CopyTo(_buffer.AsSpan(startPos));
			if (pass2Count > 0) {
				inputBuffer.Slice(pass1Count, pass2Count).CopyTo(_buffer.AsSpan());
			}

			if (pass2Count == 0) {
				// did not wrap around
				if (_availableBuffer != _size) {
					// have never wrapped around
					_availableBuffer += count;
				} else {
					_startBufferOffset += count;
					StartPosition += count;
				}
			} else {
				// wrapped around
				if (_availableBuffer == _size) {
					StartPosition += count;
				} else {
					// first time wrap-around
					StartPosition += pass2Count;
				}

				_startBufferOffset = pass2Count;
				_availableBuffer = _size;
			}

			return count;
		}

		/// <summary>
		/// Read from the Buffer
		/// </summary>
		public bool Read(Span<float> outBuffer, long startRead, int readCount) {
			var endRead = (int) (startRead + readCount);
			var endAvailable = (int) (StartPosition + _availableBuffer);

			if (startRead < StartPosition || endRead > endAvailable) {
				return false;
			}

			var startReadPos = (int) ((startRead - StartPosition + _startBufferOffset) % _size);
			var block1Samples = Mathf.Min(readCount, _size - startReadPos);
			var block2Samples = readCount - block1Samples;

			_buffer.AsSpan(startReadPos, block1Samples).CopyTo(outBuffer);
			if (block2Samples > 0) {
				_buffer.AsSpan(0, block2Samples).CopyTo(outBuffer[block1Samples..]);
			}

			return true;
		}
	}
}