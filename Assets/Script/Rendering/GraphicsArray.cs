using System;
using UnityEngine;

namespace YARG.Rendering
{
    /// <summary>
    /// A dynamically-resizing wrapper around <see cref="GraphicsBuffer"/>.
    /// </summary>
    // The "array" name is a little bit of a misnomer; it's more like a List<T>,
    // but GraphicsList<T> doesn't really sound right, and GraphicsBuffer<T> is bound to be confusing
    public class GraphicsArray<T> : IDisposable
        where T : unmanaged
    {
        private const int DEFAULT_CAPACITY = 16;

        private GraphicsBuffer _buffer;
        private readonly GraphicsBuffer.Target _target;

        private T[] _values;

        public int Count { get; private set; }
        public int MaxCapacity { get; }

        /// <summary>
        /// Retrieves the item at the given index.
        /// </summary>
        /// <remarks>
        /// You must be sure that no operations that modify the collection occur while a reference is active!
        /// Any operation that causes the backing array to be resized will cause the reference to become outdated.
        /// </remarks>
        public ref T this[int index] => ref _values[index];

        public GraphicsArray(int initialCapacity, int maxCapacity, GraphicsBuffer.Target target)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Initial capacity must be non-negative.");
            if (maxCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "Max capacity must be at least 1.");
            if (maxCapacity < initialCapacity)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "Max capacity must be greater than initial capacity.");

            if (initialCapacity < DEFAULT_CAPACITY && maxCapacity >= DEFAULT_CAPACITY)
                initialCapacity = DEFAULT_CAPACITY;

            _target = target;
            MaxCapacity = maxCapacity;
            Resize(initialCapacity);
        }

        // No accompanying finalizer, GraphicsBuffer will finalize itself
        public void Dispose()
        {
            _buffer.Dispose();
        }

        public void Add(T value)
        {
            CheckCapacity();

            if (Count >= _values.Length)
                Resize(_values.Length * 2);

            _values[Count++] = value;
        }

        private void Resize(int size)
        {
            if (size > MaxCapacity)
                size = MaxCapacity;

            Array.Resize(ref _values, size);

            _buffer?.Dispose();
            unsafe { _buffer = new(_target, size, sizeof(T)); }
        }

        public void RemoveRange(int index, int count)
        {
            CheckIndex(index, nameof(index));
            CheckIndex(index + count, nameof(count));

            var span = _values.AsSpan();
            span[(index + count)..].CopyTo(span[index..]);

            Count -= count;
        }

        public void Clear()
        {
            Count = 0;
        }

        private void CheckIndex(int index, string name)
        {
            if (index < 0 || index >= _values.Length)
                throw new ArgumentOutOfRangeException(name);
        }

        private void CheckCapacity()
        {
            if (Count >= MaxCapacity)
                throw new InvalidOperationException("Cannot exceed maximum capacity of the collection.");
        }

        public void WriteTo(MaterialPropertyBlock properties, string property)
        {
            _buffer.SetData(_values);
            properties.SetBuffer(property, _buffer);
        }

        public void WriteTo(MaterialPropertyBlock properties, int property)
        {
            _buffer.SetData(_values);
            properties.SetBuffer(property, _buffer);
        }
    }

    public static class GraphicsArrayExtensions
    {
        public static void SetBuffer<T>(this MaterialPropertyBlock properties, string property, GraphicsArray<T> array)
            where T : unmanaged
        {
            array.WriteTo(properties, property);
        }

        public static void SetBuffer<T>(this MaterialPropertyBlock properties, int property, GraphicsArray<T> array)
            where T : unmanaged
        {
            array.WriteTo(properties, property);
        }
    }
}
