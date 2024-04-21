using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Rendering
{
    public class MeshInstancer : IDisposable
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct InstancedTransform
        {
            public Vector4 position;
            public Quaternion rotation;
            public Vector4 scale;
        };

        private static readonly int _transformsProperty = Shader.PropertyToID("_InstancedTransform");
        private static readonly int _colorsProperty = Shader.PropertyToID("_InstancedColor");

        private readonly Mesh _mesh;
        private readonly Material _material;

        private readonly MaterialPropertyBlock _properties = new();

        private readonly GraphicsArray<InstancedTransform> _transforms;
        private readonly GraphicsArray<Vector4> _colors;

        public int InstanceCount => _transforms.Count;
        public bool AtCapacity => _transforms.Count >= _transforms.MaxCapacity;

        public MeshInstancer(Mesh mesh, Material material, int instanceLimit)
        {
            _mesh = mesh;
            _material = material;

            _transforms = new(64, instanceLimit, GraphicsBuffer.Target.Structured);
            _colors = new(64, instanceLimit, GraphicsBuffer.Target.Structured);
        }

        public void Dispose()
        {
            _transforms.Dispose();
            _colors.Dispose();
        }

        public void BeginDraw()
        {
            _transforms.Clear();
            _colors.Clear();
        }

        public void EndDraw(Bounds bounds, Camera camera = null, int layer = 0,
            ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
        {
            if (_transforms.Count < 1)
                return;

            _properties.SetBuffer(_transformsProperty, _transforms);
            _properties.SetBuffer(_colorsProperty, _colors);

            for (int subMesh = 0; subMesh < _mesh.subMeshCount; subMesh++)
            {
                Graphics.DrawMeshInstancedProcedural(
                    _mesh, subMesh, _material, bounds, InstanceCount, _properties,
                    shadowMode, receiveShadows, layer, camera, lightProbing, lightProxy
                );
            }
        }

        public void RenderInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            _transforms.Add(new()
            {
                position = position,
                rotation = rotation,
                scale = scale,
            });
            _colors.Add(color);
        }
    }
}