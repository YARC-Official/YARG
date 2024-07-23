using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Rendering
{
    /// <summary>
    /// A wrapper around a <see cref="Renderer"/> and a <see cref="MaterialPropertyBlock"/>
    /// which ensures both stay in sync with each other.
    /// </summary>
    public class RendererPropertyWrapper
    {
        /// <summary>
        /// Defers setting the property block until the end of a <c>using</c> statement.
        /// </summary>
        /// <see cref="SetMultiple"/>
        public ref struct SetMultipleBarrier
        {
            private RendererPropertyWrapper _wrapper;

            public SetMultipleBarrier(RendererPropertyWrapper wrapper)
            {
                _wrapper = wrapper;
                _wrapper._setMultiples++;
            }

            public void Dispose()
            {
                _wrapper._setMultiples--;
                _wrapper.SetPropertyBlock();
            }
        }

        private Renderer _renderer;
        private MaterialPropertyBlock _properties;

        private int _materialIndex = -1;
        private int _setMultiples = 0;

        public RendererPropertyWrapper(Renderer renderer, int materialIndex = -1)
        {
            if (materialIndex >= 0 && materialIndex >= renderer.sharedMaterials.Length)
                throw new ArgumentOutOfRangeException(nameof(materialIndex));

            _renderer = renderer;
            _materialIndex = materialIndex;

            _properties = new();
        }

        /// <summary>
        /// Defers setting the property block until the end of the <c>using</c> statement this function is used in.
        /// </summary>
        /// <remarks>
        /// Use this when setting multiple properties in quick succession, as it reduces the number of times
        /// material properties need to be copied to the renderer's property block.
        /// </remarks>
        /// <example>
        /// <code>
        /// using (properties.SetMultiple())
        /// {
        ///     properties.SetColor(colorProperty, color);
        ///     properties.SetInt(spriteIndexProperty, spriteIndex);
        ///     properties.SetVector(randomProperty, randomVector);
        /// } // Properties will be committed at the end of the block
        /// </code>
        /// </example>
        public SetMultipleBarrier SetMultiple() => new(this);

        private void SetPropertyBlock()
        {
            if (_setMultiples > 0)
                return;

            if (_materialIndex < 0)
                _renderer.SetPropertyBlock(_properties);
            else
                _renderer.SetPropertyBlock(_properties, _materialIndex);
        }

        public void SetInt(string name, int value)
        {
            _properties.SetInt(name, value);
            SetPropertyBlock();
        }

        public void SetInt(int nameID, int value)
        {
            _properties.SetInt(nameID, value);
            SetPropertyBlock();
        }

        public void SetFloat(string name, float value)
        {
            _properties.SetFloat(name, value);
            SetPropertyBlock();
        }

        public void SetFloat(int nameID, float value)
        {
            _properties.SetFloat(nameID, value);
            SetPropertyBlock();
        }

        public void SetInteger(string name, int value)
        {
            _properties.SetInteger(name, value);
            SetPropertyBlock();
        }

        public void SetInteger(int nameID, int value)
        {
            _properties.SetInteger(nameID, value);
            SetPropertyBlock();
        }

        public void SetVector(string name, Vector4 value)
        {
            _properties.SetVector(name, value);
            SetPropertyBlock();
        }

        public void SetVector(int nameID, Vector4 value)
        {
            _properties.SetVector(nameID, value);
            SetPropertyBlock();
        }

        public void SetColor(string name, Color value)
        {
            _properties.SetColor(name, value);
            SetPropertyBlock();
        }

        public void SetColor(int nameID, Color value)
        {
            _properties.SetColor(nameID, value);
            SetPropertyBlock();
        }

        public void SetMatrix(string name, Matrix4x4 value)
        {
            _properties.SetMatrix(name, value);
            SetPropertyBlock();
        }

        public void SetMatrix(int nameID, Matrix4x4 value)
        {
            _properties.SetMatrix(nameID, value);
            SetPropertyBlock();
        }

        public void SetBuffer(string name, ComputeBuffer value)
        {
            _properties.SetBuffer(name, value);
            SetPropertyBlock();
        }

        public void SetBuffer(int nameID, ComputeBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            SetPropertyBlock();
        }

        public void SetBuffer(string name, GraphicsBuffer value)
        {
            _properties.SetBuffer(name, value);
            SetPropertyBlock();
        }

        public void SetBuffer(int nameID, GraphicsBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            SetPropertyBlock();
        }

        public void SetTexture(string name, Texture value)
        {
            _properties.SetTexture(name, value);
            SetPropertyBlock();
        }

        public void SetTexture(int nameID, Texture value)
        {
            _properties.SetTexture(nameID, value);
            SetPropertyBlock();
        }

        public void SetTexture(string name, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(name, value, element);
            SetPropertyBlock();
        }

        public void SetTexture(int nameID, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(nameID, value, element);
            SetPropertyBlock();
        }

        public void SetConstantBuffer(string name, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            SetPropertyBlock();
        }

        public void SetConstantBuffer(int nameID, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            SetPropertyBlock();
        }

        public void SetConstantBuffer(string name, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            SetPropertyBlock();
        }

        public void SetConstantBuffer(int nameID, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            SetPropertyBlock();
        }

        public void SetFloatArray(string name, List<float> values)
        {
            _properties.SetFloatArray(name, values);
            SetPropertyBlock();
        }

        public void SetFloatArray(int nameID, List<float> values)
        {
            _properties.SetFloatArray(nameID, values);
            SetPropertyBlock();
        }

        public void SetFloatArray(string name, float[] values)
        {
            _properties.SetFloatArray(name, values);
            SetPropertyBlock();
        }

        public void SetFloatArray(int nameID, float[] values)
        {
            _properties.SetFloatArray(nameID, values);
            SetPropertyBlock();
        }

        public void SetVectorArray(string name, List<Vector4> values)
        {
            _properties.SetVectorArray(name, values);
            SetPropertyBlock();
        }

        public void SetVectorArray(int nameID, List<Vector4> values)
        {
            _properties.SetVectorArray(nameID, values);
            SetPropertyBlock();
        }

        public void SetVectorArray(string name, Vector4[] values)
        {
            _properties.SetVectorArray(name, values);
            SetPropertyBlock();
        }

        public void SetVectorArray(int nameID, Vector4[] values)
        {
            _properties.SetVectorArray(nameID, values);
            SetPropertyBlock();
        }

        public void SetMatrixArray(string name, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(name, values);
            SetPropertyBlock();
        }

        public void SetMatrixArray(int nameID, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(nameID, values);
            SetPropertyBlock();
        }

        public void SetMatrixArray(string name, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(name, values);
            SetPropertyBlock();
        }

        public void SetMatrixArray(int nameID, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(nameID, values);
            SetPropertyBlock();
        }
    }
}