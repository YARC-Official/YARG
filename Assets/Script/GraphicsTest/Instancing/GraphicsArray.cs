using System;
using UnityEngine;

namespace YARG.GraphicsTest.Instancing
{
    public static class GraphicsArray
    {
        public const GraphicsBuffer.Target INDIRECT_STRUCTURED =
            GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Structured;
    }

    public class GraphicsArray<T> : IDisposable
        where T : unmanaged
    {
        private readonly T[] _values;
        private readonly GraphicsBuffer _buffer;

        public unsafe GraphicsArray(int count, GraphicsBuffer.Target type)
        {
            _values = new T[count];
            _buffer = new(type, count, sizeof(T));
        }

        public ref T this[int index]
        {
            get => ref _values[index];
        }

        // No accompanying finalizer, ComputeBuffer will finalize itself
        public void Dispose()
        {
            _buffer.Dispose();
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
