using System;
using System.Collections.Generic;
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

        private List<T> _values;
        private readonly int _maxCapacity;

        public int Count => _values.Count;
        public int MaxCapacity => _maxCapacity;

        public GraphicsArray(int initialCapacity, int maxCapacity, GraphicsBuffer.Target target)
        {
            if (initialCapacity < 0)
                throw new ArgumentOutOfRangeException(nameof(initialCapacity), initialCapacity, "Initial capacity must be non-negative.");
            if (maxCapacity < 1)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "Max capacity must be at least 1.");
            if (maxCapacity < initialCapacity)
                throw new ArgumentOutOfRangeException(nameof(maxCapacity), maxCapacity, "Max capacity must be greater than initial capacity.");

            if (initialCapacity < DEFAULT_CAPACITY)
                initialCapacity = DEFAULT_CAPACITY;

            unsafe { _buffer = new(target, initialCapacity, sizeof(T)); }
            _target = target;

            _values = new(initialCapacity);
            _maxCapacity = maxCapacity;
        }

        // No accompanying finalizer, GraphicsBuffer will finalize itself
        public void Dispose()
        {
            _buffer.Dispose();
        }

        public void Add(T value)
        {
            CheckCount();
            _values.Add(value);

            // Prevent the list from going over capacity
            if (_values.Capacity > _maxCapacity)
                _values.Capacity = _maxCapacity;

            // Resize the buffer if needed
            if (_values.Capacity > _buffer.count)
            {
                _buffer.Dispose();
                unsafe { _buffer = new(_target, _values.Capacity, sizeof(T)); }
            }
        }

        public void Clear()
        {
            _values.Clear();
        }

        private void CheckCount()
        {
            if (_values.Count >= _maxCapacity)
                throw new InvalidOperationException("Cannot exceed maximum capacity of the buffer.");
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

    public static class ComputeArrayExtensions
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
