using System;
using System.Collections;
using System.Collections.Generic;

namespace YARG.Menu.Persistent
{
    public class QueueBuffer<T> : IEnumerable<T>
    {
        private readonly Queue<T> _queue;
        public           int      MaxSize { get; }

        public QueueBuffer(int maxSize)
        {
            if (maxSize <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxSize), "MaxSize must be greater than 0.");
            }

            _queue = new Queue<T>(maxSize);
            MaxSize = maxSize;
        }

        public int Count => _queue.Count;

        public void Add(T item)
        {
            if (_queue.Count >= MaxSize)
            {
                _queue.Dequeue();
            }

            _queue.Enqueue(item);
        }

        public IEnumerator<T> GetEnumerator() => _queue.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}