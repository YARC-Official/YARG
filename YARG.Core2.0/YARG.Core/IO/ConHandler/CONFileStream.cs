using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace YARG.Core.IO
{
    public sealed class CONFileStream : Stream
    {
        private const int FIRSTBLOCK_OFFSET = 0xC000;
        private const int BYTES_PER_BLOCK = 0x1000;
        private const int BLOCKS_PER_SECTION = 170;
        private const int BYTES_PER_SECTION = BLOCKS_PER_SECTION * BYTES_PER_BLOCK;
        private const int NUM_BLOCKS_SQUARED = BLOCKS_PER_SECTION * BLOCKS_PER_SECTION;

        private const int HASHBLOCK_OFFSET = 4075;
        private const int DIST_PER_HASH = 4072;

        public static FixedArray<byte> LoadFile(string file, bool isContinguous, int fileSize, int blockNum, int shift)
        {
            using FileStream filestream = new(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return LoadFile(filestream, isContinguous, fileSize, blockNum, shift);
        }

        public static FixedArray<byte> LoadFile(Stream filestream, bool isContinguous, int fileSize, int blockNum, int shift)
        {
            var data = FixedArray<byte>.Alloc(fileSize);
            if (isContinguous)
            {
                long skipVal = BYTES_PER_BLOCK << shift;
                int threshold = blockNum - blockNum % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
                int numBlocks = BLOCKS_PER_SECTION - blockNum % BLOCKS_PER_SECTION;
                int readSize = BYTES_PER_BLOCK * numBlocks;
                int offset = 0;

                filestream.Seek(CalculateBlockLocation(blockNum, shift), SeekOrigin.Begin);
                while (true)
                {
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    if (filestream.Read(data.Slice(offset, readSize)) != readSize)
                    {
                        data.Dispose();
                        throw new Exception("Read error in CON-like subfile - Type: Contiguous");
                    }

                    offset += readSize;
                    if (offset == fileSize)
                        break;

                    blockNum += numBlocks;
                    numBlocks = BLOCKS_PER_SECTION;
                    readSize = BYTES_PER_SECTION;

                    int seekCount = 1;
                    if (blockNum == BLOCKS_PER_SECTION)
                        seekCount = 2;
                    else if (blockNum == threshold)
                    {
                        if (blockNum == NUM_BLOCKS_SQUARED)
                            seekCount = 2;
                        ++seekCount;
                        threshold += NUM_BLOCKS_SQUARED;
                    }

                    filestream.Seek(seekCount * skipVal, SeekOrigin.Current);
                }
            }
            else
            {
                Span<byte> buffer = stackalloc byte[3];
                int offset = 0;
                while (true)
                {
                    long blockLocation = CalculateBlockLocation(blockNum, shift);

                    int readSize = BYTES_PER_BLOCK;
                    if (readSize > fileSize - offset)
                        readSize = fileSize - offset;

                    filestream.Seek(blockLocation, SeekOrigin.Begin);
                    if (filestream.Read(data.Slice(offset, readSize)) != readSize)
                    {
                        data.Dispose();
                        throw new Exception("Pre-Read error in CON-like subfile - Type: Split");
                    }

                    offset += readSize;
                    if (offset == fileSize)
                        break;

                    long hashlocation = blockLocation - ((long) (blockNum % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                    filestream.Seek(hashlocation, SeekOrigin.Begin);
                    if (filestream.Read(buffer) != buffer.Length)
                    {
                        data.Dispose();
                        throw new Exception("Post-Read error in CON-like subfile - Type: Split");
                    }
                    blockNum = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                }
            }
            return data;
        }

        public static long CalculateBlockLocation(int blockNum, int shift)
        {
            int blockAdjust = 0;
            if (blockNum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blockNum / BLOCKS_PER_SECTION + 1 << shift;
                if (blockNum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blockNum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return FIRSTBLOCK_OFFSET + (long) (blockAdjust + blockNum) * BYTES_PER_BLOCK;
        }

        private readonly FileStream _filestream;
        private readonly long _fileSize;
        private readonly FixedArray<byte> _dataBuffer;
        private readonly FixedArray<long> _blockLocations;
        private readonly int _initialOffset;

        private long _bufferPosition;
        private long _position;
        private long _blockIndex = 0;
        private bool _disposedStream;

        public override bool CanRead => _filestream.CanRead;
        public override bool CanWrite => false;
        public override bool CanSeek => _filestream.CanSeek;
        public override long Length => _fileSize;

        public override long Position
        {
            get => _position;
            set
            {
                if (value < 0 || value > _fileSize) throw new ArgumentOutOfRangeException();

                _position = value;
                if (value == _fileSize)
                    return;

                int truePosition = (int)(value + _initialOffset);
                _blockIndex = truePosition / _dataBuffer.Length;
                _bufferPosition = truePosition % _dataBuffer.Length;

                long readSize = _dataBuffer.Length - _bufferPosition;
                if (readSize > _fileSize - _position)
                    readSize = (int)(_fileSize - _position);

                if (_blockIndex < _blockLocations.Length)
                {
                    long offset = _blockIndex == 0 ? value : _bufferPosition;
                    _filestream.Seek(_blockLocations[(int)_blockIndex++] + offset, SeekOrigin.Begin);
                    var buffer = _dataBuffer.Slice(_bufferPosition, readSize);
                    if (_filestream.Read(buffer) != readSize)
                        throw new Exception("Seek error in CON subfile");
                }
            }
        }

        public CONFileStream(string file, bool isContinguous, int fileSize, int firstBlock, int shift)
            : this(new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1), isContinguous, fileSize, firstBlock, shift) { }

        public CONFileStream(FileStream filestream, bool isContinguous, int fileSize, int firstBlock, int shift)
        {
            _filestream = filestream;
            this._fileSize = fileSize;

            int block = firstBlock;
            if (isContinguous)
            {
                int blockOffset = firstBlock % BLOCKS_PER_SECTION;
                _initialOffset = blockOffset * BYTES_PER_BLOCK;

                int totalSpace = fileSize + _initialOffset;
                int numBlocks = totalSpace % BYTES_PER_SECTION == 0 ? totalSpace / BYTES_PER_SECTION : totalSpace / BYTES_PER_SECTION + 1;
                using var blockLocations = FixedArray<long>.Alloc(numBlocks);

                int blockMovement = BLOCKS_PER_SECTION - blockOffset;
                int byteMovement = blockMovement * BYTES_PER_BLOCK;
                int skipVal = BYTES_PER_BLOCK << shift;
                int threshold = firstBlock - firstBlock % NUM_BLOCKS_SQUARED + NUM_BLOCKS_SQUARED;
                long location = CalculateBlockLocation(firstBlock, shift);
                for (int i = 0; i < numBlocks; i++)
                {
                    unsafe
                    {
                        blockLocations.Ptr[i] = location;
                    }

                    if (i < numBlocks - 1)
                    {
                        block += blockMovement;

                        int seekCount = 1;
                        if (block == BLOCKS_PER_SECTION)
                            seekCount = 2;
                        else if (block == threshold)
                        {
                            if (block == NUM_BLOCKS_SQUARED)
                                seekCount = 2;
                            ++seekCount;
                            threshold += NUM_BLOCKS_SQUARED;
                        }

                        location += byteMovement + seekCount * skipVal;
                        blockMovement = BLOCKS_PER_SECTION;
                        byteMovement = BYTES_PER_SECTION;
                    }
                }
                _blockLocations = blockLocations.TransferOwnership();
                _dataBuffer = FixedArray<byte>.Alloc(BYTES_PER_SECTION);
            }
            else
            {
                int numBlocks = fileSize % BYTES_PER_BLOCK == 0 ? fileSize / BYTES_PER_BLOCK : fileSize / BYTES_PER_BLOCK + 1;
                using var blockLocations = FixedArray<long>.Alloc(numBlocks);

                Span<byte> buffer = stackalloc byte[3];
                _initialOffset = 0;
                for (int i = 0; i < numBlocks; i++)
                {
                    unsafe
                    {
                        long location = blockLocations.Ptr[i] = CalculateBlockLocation(block, shift);
                        if (i < numBlocks - 1)
                        {
                            long hashlocation = location - ((long) (block % BLOCKS_PER_SECTION) * DIST_PER_HASH + HASHBLOCK_OFFSET);
                            _filestream.Seek(hashlocation, SeekOrigin.Begin);
                            if (_filestream.Read(buffer) != 3)
                                throw new Exception("Hashblock Read error in CON subfile");

                            block = buffer[0] << 16 | buffer[1] << 8 | buffer[2];
                        }
                    }
                }
                _blockLocations = blockLocations.TransferOwnership();
                _dataBuffer = FixedArray<byte>.Alloc(BYTES_PER_BLOCK);
            }
            UpdateBuffer();
        }

        public override void Flush()
        {
            _filestream.Flush();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (offset < 0 || count < 0)
                throw new ArgumentOutOfRangeException();

            if (buffer == null)
                throw new ArgumentNullException();

            if (buffer.Length < offset + count)
                throw new ArgumentException();

            if (_position == _fileSize)
                return 0;

            long read = 0;
            long bytesLeftInSection = _dataBuffer.Length - _bufferPosition;
            if (bytesLeftInSection > _fileSize - (int) _position)
                bytesLeftInSection = _fileSize - (int) _position;

            while (read < count)
            {
                long readCount = count - read;
                if (readCount > bytesLeftInSection)
                    readCount = bytesLeftInSection;

                Unsafe.CopyBlock(ref buffer[offset + read], ref _dataBuffer[(int)_bufferPosition], (uint) readCount);

                read += readCount;
                _position += readCount;
                _bufferPosition += readCount;

                if (_bufferPosition < _dataBuffer.Length || _position == _fileSize)
                    break;

                bytesLeftInSection = UpdateBuffer();
            }
            return (int)read;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
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
                    Position = _fileSize + offset;
                    break;
            }
            return _position;
        }

        protected override void Dispose(bool disposing)
        {
            if (!_disposedStream)
            {
                if (disposing)
                {
                    _filestream.Dispose();
                    _dataBuffer.Dispose();
                    _blockLocations.Dispose();
                }
                _disposedStream = true;
            }
        }

        private long UpdateBuffer()
        {
            _bufferPosition = _blockIndex == 0 ? _initialOffset : 0;
            long readSize = _dataBuffer.Length - _bufferPosition;
            if (readSize > _fileSize - _position)
                readSize = _fileSize - _position;

            _filestream.Seek(_blockLocations[(int)_blockIndex++], SeekOrigin.Begin);
            var buffer = _dataBuffer.Slice(_bufferPosition, readSize);
            if (_filestream.Read(buffer) != readSize)
                throw new Exception("Seek error in CON subfile");
            return readSize;
        }

        private static int CalculateBlockNum(int blockNum, int shift)
        {
            int blockAdjust = 0;
            if (blockNum >= BLOCKS_PER_SECTION)
            {
                blockAdjust += blockNum / BLOCKS_PER_SECTION + 1 << shift;
                if (blockNum >= NUM_BLOCKS_SQUARED)
                    blockAdjust += blockNum / NUM_BLOCKS_SQUARED + 1 << shift;
            }
            return blockAdjust + blockNum;
        }
    }
}
