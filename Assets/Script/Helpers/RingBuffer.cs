using System;

namespace YARG.Helpers
{
    internal sealed class RingBuffer<T>
    {
        private readonly T[] _items;
        private int _start;

        public int Count { get; private set; }

        public T this[int index]
        {
            get => _items[GetIndex(index)];
            set => _items[GetIndex(index)] = value;
        }

        public RingBuffer(int capacity)
        {
            if (capacity <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }

            _items = new T[capacity];
        }

        public void Add(T item)
        {
            if (Count == _items.Length)
            {
                throw new InvalidOperationException("Ring buffer is full.");
            }

            _items[(_start + Count) % _items.Length] = item;
            Count++;
        }

        public T RemoveOldest()
        {
            if (Count == 0)
            {
                throw new InvalidOperationException("Ring buffer is empty.");
            }

            var item = _items[_start];
            _items[_start] = default;
            _start = (_start + 1) % _items.Length;
            Count--;
            return item;
        }

        public void Clear()
        {
            Array.Clear(_items, 0, _items.Length);
            _start = 0;
            Count = 0;
        }

        private int GetIndex(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return (_start + index) % _items.Length;
        }
    }
}
