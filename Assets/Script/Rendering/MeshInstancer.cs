using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Rendering
{
    public class MeshInstancer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct InstancedTransform
        {
            // Padding added to ensure consistent packing across hardware
            public Vector3 position;
            private float _padding1;
            public Quaternion rotation;
            public Vector3 scale;
            private float _padding2;
        };

        private static readonly int _transformsProperty = Shader.PropertyToID("_InstancedTransform");
        private static readonly int _colorsProperty = Shader.PropertyToID("_InstancedColor");

        private readonly MeshInfo _meshInfo;
        private readonly Material _material;

        private readonly MaterialPropertyBlock _properties = new();

        private readonly GraphicsArray<InstancedTransform> _transforms;
        private readonly GraphicsArray<Color> _colors;

        public int InstanceCount => _transforms.Count;
        public bool AtCapacity => _transforms.Count >= _transforms.MaxCapacity;

        public MeshInstancer(MeshInfo meshInfo, Material material, int initialCapacity, int instanceLimit)
        {
            _meshInfo = meshInfo;
            _material = material;

            _transforms = new(initialCapacity, instanceLimit, GraphicsBuffer.Target.Structured);
            _colors = new(initialCapacity, instanceLimit, GraphicsBuffer.Target.Structured);
        }

        public void Dispose()
        {
            _transforms.Dispose();
            _colors.Dispose();
        }

        public void AddInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            scale.Scale(_meshInfo.BaseScale);
            _transforms.Add(new()
            {
                position = position,
                rotation = rotation * _meshInfo.BaseRotation,
                scale = scale,
            });
            _colors.Add(color);
        }

        public ref Vector3 GetPosition(int index) => ref _transforms[index].position;
        public ref Quaternion GetRotation(int index) => ref _transforms[index].rotation;
        public ref Vector3 GetScale(int index) => ref _transforms[index].scale;
        public ref Color GetColor(int index) => ref _colors[index];

        public void RemoveRange(int index, int count)
        {
            _transforms.RemoveRange(index, count);
            _colors.RemoveRange(index, count);
        }

        public void Clear()
        {
            _transforms.Clear();
            _colors.Clear();
        }

        public void SetBuffer<T>(int property, GraphicsArray<T> buffer)
            where T : unmanaged
        {
            if (_material.HasBuffer(property))
                _properties.SetBuffer(property, buffer);
        }

        public void Draw(Bounds bounds, Camera camera = null, int layer = 0,
            ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
        {
            if (_transforms.Count < 1)
                return;

            _properties.SetBuffer(_transformsProperty, _transforms);
            _properties.SetBuffer(_colorsProperty, _colors);

            for (int subMesh = 0; subMesh < _meshInfo.Mesh.subMeshCount; subMesh++)
            {
                Graphics.DrawMeshInstancedProcedural(
                    _meshInfo.Mesh, subMesh, _material, bounds, InstanceCount, _properties,
                    shadowMode, receiveShadows, layer, camera, lightProbing, lightProxy
                );
            }
        }
    }
}