using System;

namespace YARG.Audio.PitchDetection {
	class CircularBuffer : IDisposable {
		readonly int m_bufSize;
		int _mBegBufOffset, _mAvailBuf;
		float[] _mBuffer;

		public CircularBuffer(int BufferCount) {
			m_bufSize = BufferCount;

			if (m_bufSize > 0)
				_mBuffer = new float[m_bufSize];
		}

		public void Dispose() => _mBuffer = null;

		/// <summary>
		/// Reset to the beginning of the Buffer
		/// </summary>
		public void Reset() => StartPosition = _mBegBufOffset = _mAvailBuf = 0;

		/// <summary>
		/// Clear the Buffer
		/// </summary>
		public void Clear() => Array.Clear(_mBuffer, 0, _mBuffer.Length);

		/// <summary>
		/// Get or set the start position
		/// </summary>
		public long StartPosition { get; set; }

		/// <summary>
		/// Get or set the amount of avaliable space
		/// </summary>
		public int Available {
			get { return _mAvailBuf; }
			set { _mAvailBuf = Math.Min(value, m_bufSize); }
		}

		/// <summary>
		/// Write data into the Buffer
		/// </summary>
		public int Write(float[] m_pInBuffer, int count) {
			count = Math.Min(count, m_bufSize);

			var startPos = _mAvailBuf != m_bufSize ? _mAvailBuf : _mBegBufOffset;
			var pass1Count = Math.Min(count, m_bufSize - startPos);
			var pass2Count = count - pass1Count;

			Array.Copy(m_pInBuffer, 0, _mBuffer, startPos, pass1Count);

			if (pass2Count > 0)
				Array.Copy(m_pInBuffer, pass1Count, _mBuffer, 0, pass2Count);

			if (pass2Count == 0) {
				// did not wrap around
				if (_mAvailBuf != m_bufSize) _mAvailBuf += count; // have never wrapped around
				else {
					_mBegBufOffset += count;
					StartPosition += count;
				}
			} else {
				// wrapped around
				if (_mAvailBuf != m_bufSize)
					StartPosition += pass2Count; // first time wrap-around
				else StartPosition += count;

				_mBegBufOffset = pass2Count;
				_mAvailBuf = m_bufSize;
			}

			return count;
		}

		/// <summary>
		/// Read from the Buffer
		/// </summary>
		public bool Read(float[] outBuffer, long startRead, int readCount) {
			var endRead = (int) (startRead + readCount);
			var endAvail = (int) (StartPosition + _mAvailBuf);

			if (startRead < StartPosition || endRead > endAvail) return false;

			var startReadPos = (int) ((startRead - StartPosition + _mBegBufOffset) % m_bufSize);
			var block1Samples = Math.Min(readCount, m_bufSize - startReadPos);
			var block2Samples = readCount - block1Samples;

			Array.Copy(_mBuffer, startReadPos, outBuffer, 0, block1Samples);

			if (block2Samples > 0)
				Array.Copy(_mBuffer, 0, outBuffer, block1Samples, block2Samples);

			return true;
		}
	}
}