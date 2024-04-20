using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Logging;

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

        private readonly int _instanceLimit;

        private readonly MaterialPropertyBlock _properties = new();

        private readonly GraphicsArray<InstancedTransform> _transforms;
        private readonly GraphicsArray<Vector4> _colors;

        public int InstanceCount { get; private set; }
        public bool AtCapacity => InstanceCount >= _instanceLimit;

        public MeshInstancer(Mesh mesh, Material material, int instanceLimit)
        {
            _mesh = mesh;
            _material = material;

            _instanceLimit = instanceLimit;

            _transforms = new(instanceLimit, GraphicsBuffer.Target.Structured);
            _colors = new(instanceLimit, GraphicsBuffer.Target.Structured);
        }

        public void Dispose()
        {
            _transforms.Dispose();
            _colors.Dispose();
        }

        public void BeginDraw()
        {
            InstanceCount = 0;
        }

        public void EndDraw(Bounds bounds, Camera camera = null, int layer = 0,
            ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
        {
            if (InstanceCount < 1)
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
            if (InstanceCount >= _instanceLimit)
            {
                YargLogger.LogDebug("Attempted to add an instance above the limit!");
                return;
            }

            _transforms[InstanceCount] = new()
            {
                position = position,
                rotation = rotation,
                scale = scale,
            };
            _colors[InstanceCount] = color;

            InstanceCount++;
        }
    }
}