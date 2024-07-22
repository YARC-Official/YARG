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
        private Renderer _renderer;
        private MaterialPropertyBlock _properties;

        public RendererPropertyWrapper(Renderer renderer)
        {
            _renderer = renderer;
            _properties = new();
        }

        #region Per Renderer
        public void SetInt(string name, int value)
        {
            _properties.SetInt(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetInt(int nameID, int value)
        {
            _properties.SetInt(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloat(string name, float value)
        {
            _properties.SetFloat(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloat(int nameID, float value)
        {
            _properties.SetFloat(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetInteger(string name, int value)
        {
            _properties.SetInteger(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetInteger(int nameID, int value)
        {
            _properties.SetInteger(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVector(string name, Vector4 value)
        {
            _properties.SetVector(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVector(int nameID, Vector4 value)
        {
            _properties.SetVector(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetColor(string name, Color value)
        {
            _properties.SetColor(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetColor(int nameID, Color value)
        {
            _properties.SetColor(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrix(string name, Matrix4x4 value)
        {
            _properties.SetMatrix(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrix(int nameID, Matrix4x4 value)
        {
            _properties.SetMatrix(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetBuffer(string name, ComputeBuffer value)
        {
            _properties.SetBuffer(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetBuffer(int nameID, ComputeBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetBuffer(string name, GraphicsBuffer value)
        {
            _properties.SetBuffer(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetBuffer(int nameID, GraphicsBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetTexture(string name, Texture value)
        {
            _properties.SetTexture(name, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetTexture(int nameID, Texture value)
        {
            _properties.SetTexture(nameID, value);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetTexture(string name, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(name, value, element);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetTexture(int nameID, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(nameID, value, element);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetConstantBuffer(string name, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetConstantBuffer(int nameID, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetConstantBuffer(string name, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetConstantBuffer(int nameID, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloatArray(string name, List<float> values)
        {
            _properties.SetFloatArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloatArray(int nameID, List<float> values)
        {
            _properties.SetFloatArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloatArray(string name, float[] values)
        {
            _properties.SetFloatArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetFloatArray(int nameID, float[] values)
        {
            _properties.SetFloatArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVectorArray(string name, List<Vector4> values)
        {
            _properties.SetVectorArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVectorArray(int nameID, List<Vector4> values)
        {
            _properties.SetVectorArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVectorArray(string name, Vector4[] values)
        {
            _properties.SetVectorArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetVectorArray(int nameID, Vector4[] values)
        {
            _properties.SetVectorArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrixArray(string name, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrixArray(int nameID, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrixArray(string name, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(name, values);
            _renderer.SetPropertyBlock(_properties);
        }

        public void SetMatrixArray(int nameID, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(nameID, values);
            _renderer.SetPropertyBlock(_properties);
        }
        #endregion

        #region Per Material
        public void SetInt(string name, int materialIndex, int value)
        {
            _properties.SetInt(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetInt(int nameID, int materialIndex, int value)
        {
            _properties.SetInt(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloat(string name, int materialIndex, float value)
        {
            _properties.SetFloat(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloat(int nameID, int materialIndex, float value)
        {
            _properties.SetFloat(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetInteger(string name, int materialIndex, int value)
        {
            _properties.SetInteger(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetInteger(int nameID, int materialIndex, int value)
        {
            _properties.SetInteger(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVector(string name, int materialIndex, Vector4 value)
        {
            _properties.SetVector(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVector(int nameID, int materialIndex, Vector4 value)
        {
            _properties.SetVector(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetColor(string name, int materialIndex, Color value)
        {
            _properties.SetColor(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetColor(int nameID, int materialIndex, Color value)
        {
            _properties.SetColor(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrix(string name, int materialIndex, Matrix4x4 value)
        {
            _properties.SetMatrix(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrix(int nameID, int materialIndex, Matrix4x4 value)
        {
            _properties.SetMatrix(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetBuffer(string name, int materialIndex, ComputeBuffer value)
        {
            _properties.SetBuffer(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetBuffer(int nameID, int materialIndex, ComputeBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetBuffer(string name, int materialIndex, GraphicsBuffer value)
        {
            _properties.SetBuffer(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetBuffer(int nameID, int materialIndex, GraphicsBuffer value)
        {
            _properties.SetBuffer(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetTexture(string name, int materialIndex, Texture value)
        {
            _properties.SetTexture(name, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetTexture(int nameID, int materialIndex, Texture value)
        {
            _properties.SetTexture(nameID, value);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetTexture(string name, int materialIndex, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(name, value, element);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetTexture(int nameID, int materialIndex, RenderTexture value, RenderTextureSubElement element)
        {
            _properties.SetTexture(nameID, value, element);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetConstantBuffer(string name, int materialIndex, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetConstantBuffer(int nameID, int materialIndex, ComputeBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetConstantBuffer(string name, int materialIndex, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(name, value, offset, size);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetConstantBuffer(int nameID, int materialIndex, GraphicsBuffer value, int offset, int size)
        {
            _properties.SetConstantBuffer(nameID, value, offset, size);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloatArray(string name, int materialIndex, List<float> values)
        {
            _properties.SetFloatArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloatArray(int nameID, int materialIndex, List<float> values)
        {
            _properties.SetFloatArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloatArray(string name, int materialIndex, float[] values)
        {
            _properties.SetFloatArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetFloatArray(int nameID, int materialIndex, float[] values)
        {
            _properties.SetFloatArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVectorArray(string name, int materialIndex, List<Vector4> values)
        {
            _properties.SetVectorArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVectorArray(int nameID, int materialIndex, List<Vector4> values)
        {
            _properties.SetVectorArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVectorArray(string name, int materialIndex, Vector4[] values)
        {
            _properties.SetVectorArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetVectorArray(int nameID, int materialIndex, Vector4[] values)
        {
            _properties.SetVectorArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrixArray(string name, int materialIndex, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrixArray(int nameID, int materialIndex, List<Matrix4x4> values)
        {
            _properties.SetMatrixArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrixArray(string name, int materialIndex, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(name, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }

        public void SetMatrixArray(int nameID, int materialIndex, Matrix4x4[] values)
        {
            _properties.SetMatrixArray(nameID, values);
            _renderer.SetPropertyBlock(_properties, materialIndex);
        }
        #endregion
    }
}