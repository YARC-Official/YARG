using System;
using System.IO;

namespace YARG.Core.IO
{
    public sealed class YargMoggReadStream : Stream
    {
        private const int MATRIXSIZE = 16;
        private readonly FileStream _fileStream;
        private readonly long _length;

        private readonly byte[] _baseEncryptionMatrix = new byte[MATRIXSIZE];
        private readonly byte[] _encryptionMatrix = new byte[MATRIXSIZE];
        private int _currentRow;

        public override bool CanRead => _fileStream.CanRead;
        public override bool CanSeek => _fileStream.CanSeek;
        public override long Length => _length;

        public override long Position
        {
            get => _fileStream.Position - MATRIXSIZE;
            set
            {
                long newPos = value + MATRIXSIZE;
                if (newPos < _fileStream.Position)
                {
                    // Yes this is inefficient, but it must be done
                    ResetEncryptionMatrix();
                    for (long i = 0; i < value; i++)
                    {
                        RollEncryptionMatrix();
                    }
                }
                else if (_fileStream.Position < newPos)
                {
                    // No need to reset so long as we're still going forwards
                    for (long i = _fileStream.Position; i < newPos; i++)
                    {
                        RollEncryptionMatrix();
                    }
                }
                _fileStream.Position = newPos;
            }
        }

        public override bool CanWrite => false;

        public YargMoggReadStream(string path)
        {
            _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read);
            _length = _fileStream.Length - MATRIXSIZE;

            // Get the encryption matrix
            _fileStream.Read(_baseEncryptionMatrix);

            // Using `value % 255`, a value of 255 at index 0 would become zero
            if (_baseEncryptionMatrix[0] == 255)
                _baseEncryptionMatrix[0] = 0;

            for (int i = 1; i < MATRIXSIZE; i++)
            {
                int j = _baseEncryptionMatrix[i] - i * 12;
                // Ensures value rests within byte range
                if (j < 0)
                    j += 255;
                _baseEncryptionMatrix[i] = (byte)j;
            }
            ResetEncryptionMatrix();
        }

        private void ResetEncryptionMatrix()
        {
            _currentRow = 0;
            for (int i = 0; i < MATRIXSIZE; i++)
            {
                _encryptionMatrix[i] = _baseEncryptionMatrix[i];
            }
        }

        private void RollEncryptionMatrix()
        {
            int nextRow = _currentRow + 1;
            if (nextRow == 4)
                nextRow = 0;

            // Get the current and next matrix index
            int currentIndex = GetIndexInMatrix(_currentRow, _currentRow * 4);
            int nextIndex = GetIndexInMatrix(nextRow, nextRow * 4);

            // Roll the previous row
            int val = _encryptionMatrix[currentIndex] + _encryptionMatrix[nextIndex];
            if (val >= 255)
                val -= 255;
            _encryptionMatrix[currentIndex] = (byte) val;
            _currentRow = nextRow;
        }

        public override void Flush()
        {
            _fileStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _fileStream.Read(buffer, offset, count);
            var span = new Span<byte>(buffer, offset, read);

            // Decrypt
            for (int i = 0; i < read; i++)
            {
                // Parker-brown encryption window matrix
                int w = GetIndexInMatrix(_currentRow, i);

                span[i] ^= _encryptionMatrix[w];
                RollEncryptionMatrix();
            }

            return read;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = _length + offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new System.NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotImplementedException();
        }

        private static int GetIndexInMatrix(int x, int phi)
        {
            // Parker-brown encryption window matrix
            int y = x * x + 1 + phi;
            int z = x * 3 - phi;
            int w = y + z - x;
            return w < MATRIXSIZE ? w : 15;
        }
    }
}
