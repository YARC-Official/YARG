using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.GraphicsTest.Instancing
{
    public class IndirectMeshInstancer : MeshInstancer, IDisposable
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

        private readonly Bounds _bounds;

        private readonly GraphicsArray<InstancedTransform> _transforms;
        private readonly GraphicsArray<Vector4> _colors;

        public IndirectMeshInstancer(Mesh mesh, Material material, int instanceLimit, Bounds bounds,
            int layer = 0, ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
            : base(mesh, material, instanceLimit, layer, shadowMode, receiveShadows, lightProbing, lightProxy)
        {
            _bounds = bounds;

            _transforms = new(instanceLimit, GraphicsBuffer.Target.Structured);
            _colors = new(instanceLimit, GraphicsBuffer.Target.Structured);
        }

        protected override void DisposeManaged()
        {
            _transforms.Dispose();
            _colors.Dispose();
        }

        protected override void OnEndDraw()
        {
            _properties.SetBuffer(_transformsProperty, _transforms);
            _properties.SetBuffer(_colorsProperty, _colors);

            for (int subMesh = 0; subMesh < _mesh.subMeshCount; subMesh++)
            {
                Graphics.DrawMeshInstancedProcedural(
                    _mesh, subMesh, _material, _bounds, InstanceCount, _properties,
                    _shadowMode, _receiveShadows, _layer, null, _lightProbing, _lightProxy
                );
            }
        }

        protected override void OnRenderInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            _transforms[InstanceCount] = new()
            {
                position = position,
                rotation = rotation,
                scale = scale,
            };
            _colors[InstanceCount] = color;
        }
    }
}