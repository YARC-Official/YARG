using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Minis
{
    // Borrowed from https://github.com/TheNathannator/HIDrogen
    /// <summary>
    /// A slim, no-nonsense implementation for a buffer of native memory.
    /// </summary>
    internal unsafe class SlimNativeBuffer<T> : IDisposable
        where T : unmanaged
    {
        private const Allocator kAllocator = Allocator.Persistent;

        private T* _buffer;
        private long _length;
        private int _count;

        public T* bufferPtr => _buffer;
        public int count => _count;
        public long length => _length;

        public SlimNativeBuffer(int count, int alignment = 1)
        {
            if (count < 1)
                throw new ArgumentOutOfRangeException(nameof(count), count, "Must allocate at least one element!");
            if (alignment < 1)
                throw new ArgumentOutOfRangeException(nameof(alignment), alignment, "Must align to at least a byte boundary!");

            _count = count;
            _length = count * sizeof(T);
            _buffer = (T*)UnsafeUtility.Malloc(_length, alignment, kAllocator);
            if (_buffer == null)
                throw new OutOfMemoryException("Failed to allocate memory for the buffer!");
            UnsafeUtility.MemClear(_buffer, _length);
        }

        ~SlimNativeBuffer()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_buffer != null)
            {
                UnsafeUtility.Free(_buffer, kAllocator);
                _buffer = null;
            }

            _length = 0;
            _count = 0;
        }
    }
}