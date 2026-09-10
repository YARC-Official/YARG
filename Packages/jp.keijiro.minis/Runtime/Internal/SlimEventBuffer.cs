using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;

namespace Minis
{
    // Borrowed from https://github.com/TheNathannator/HIDrogen
    /// <summary>
    /// A buffer of memory holding a sequence of <see cref="InputEvent">input events</see>.
    /// Heavily modified and slimmed down from <see cref="InputEventBuffer"/>.
    /// </summary>
    internal unsafe class SlimEventBuffer : IEnumerable<InputEventPtr>, IDisposable
    {
        private static readonly unsafe int _baseEventSize = sizeof(InputEvent); // InputEvent.kBaseEventSize
        private const int kEventAlignment = 4; // InputEvent.kAlignment

        private SlimNativeBuffer<byte> _buffer = new SlimNativeBuffer<byte>(2048);
        private InputEvent* _firstEvent => _buffer != null ? (InputEvent*)_buffer.bufferPtr : null;

        private long _usedLength;
        private int _eventCount;

        public void AppendEvent(InputEvent* eventPtr, int capacityIncrementInBytes = 2048)
        {
            if (eventPtr == null)
                throw new ArgumentNullException(nameof(eventPtr));

            // Allocate space
            var eventSizeInBytes = eventPtr->sizeInBytes;
            var destinationPtr = AllocateEvent((int)eventSizeInBytes, capacityIncrementInBytes);

            // Copy event
            UnsafeUtility.MemCpy(destinationPtr, eventPtr, eventSizeInBytes);
        }

        public InputEvent* AllocateEvent(int sizeInBytes, int capacityIncrementInBytes = 2048)
        {
            if (sizeInBytes < _baseEventSize)
                throw new ArgumentException(
                    $"sizeInBytes must be >= sizeof(InputEvent) == {_baseEventSize} (was {sizeInBytes})", nameof(sizeInBytes));

            var alignedSizeInBytes = sizeInBytes.AlignToMultipleOf(kEventAlignment);

            // Re-allocate the buffer if necessary
            var necessaryCapacity = _usedLength + alignedSizeInBytes;
            var currentCapacity = _buffer?.length ?? 0;
            if (currentCapacity < necessaryCapacity)
            {
                var newCapacity = necessaryCapacity.AlignToMultipleOf(capacityIncrementInBytes);
                if (newCapacity > int.MaxValue)
                    throw new NotImplementedException("RawBuffer long support");

                var newBuffer = new SlimNativeBuffer<byte>((int)newCapacity);
                if (_buffer != null)
                {
                    UnsafeUtility.MemCpy(newBuffer.bufferPtr, _buffer.bufferPtr, _usedLength);
                    _buffer.Dispose();
                }

                _buffer = newBuffer;
            }

            // Retrieve pointer to next available unallocated spot
            var eventPtr = (InputEvent*)(_buffer.bufferPtr + _usedLength);
            eventPtr->sizeInBytes = (uint)sizeInBytes;
            _usedLength += alignedSizeInBytes;
            ++_eventCount;

            return eventPtr;
        }

        public void Reset()
        {
            _eventCount = 0;
            _usedLength = 0;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<InputEventPtr> IEnumerable<InputEventPtr>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _buffer?.Dispose();
                _buffer = null;
            }

            _usedLength = 0;
            _eventCount = 0;
        }

        public struct Enumerator : IEnumerator<InputEventPtr>
        {
            private readonly InputEvent* m_Buffer;
            private readonly int _eventCount;
            private InputEvent* _currentEvent;
            private int _currentIndex;

            public Enumerator(SlimEventBuffer buffer)
            {
                m_Buffer = buffer._firstEvent;
                _eventCount = buffer._eventCount;
                _currentEvent = null;
                _currentIndex = 0;
            }

            public bool MoveNext()
            {
                if (_currentIndex == _eventCount)
                    return false;

                if (_currentEvent == null)
                {
                    _currentEvent = m_Buffer;
                    return _currentEvent != null;
                }

                Debug.Assert(_currentEvent != null, "Current event must not be null");

                ++_currentIndex;
                if (_currentIndex == _eventCount)
                    return false;

                _currentEvent = GetNextInMemory(_currentEvent);
                return true;
            }

            // Copied from InputEvent.GetNextInMemory
            private static InputEventPtr GetNextInMemory(InputEventPtr currentPtr)
            {
                Debug.Assert(currentPtr != null, "Event pointer must not be null!");
                var alignedSizeInBytes = currentPtr.sizeInBytes.AlignToMultipleOf(kEventAlignment);
                return (InputEvent*)((byte*)currentPtr.data + alignedSizeInBytes);
            }

            public void Reset()
            {
                _currentEvent = null;
                _currentIndex = 0;
            }

            public void Dispose()
            {
            }

            public InputEventPtr Current => _currentEvent;

            object IEnumerator.Current => Current;
        }
    }

    /// <summary>
    /// Helper methods for numeric values.
    /// Copied from UnityEngine.InputSystem.Utilities.
    /// </summary>
    internal static class NumberHelpers
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int AlignToMultipleOf(this int number, int alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long AlignToMultipleOf(this long number, long alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static uint AlignToMultipleOf(this uint number, uint alignment)
        {
            var remainder = number % alignment;
            if (remainder == 0)
                return number;

            return number + alignment - remainder;
        }
    }
}
