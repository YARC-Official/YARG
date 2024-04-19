using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.GraphicsTest.Instancing
{
    public class IndirectMeshInstancer : MeshInstancer, IDisposable
    {
        private static readonly int _transformsProperty = Shader.PropertyToID("_InstancedTransform");
        private static readonly int _colorsProperty = Shader.PropertyToID("_InstancedColor");

        private readonly Bounds _bounds;

        // TODO: Figure out how to use buffers for this
        // All current attempts have failed
        // private readonly GraphicsArray<Matrix4x4> _transforms;
        // private readonly GraphicsArray<Vector4> _colors;

        private readonly Matrix4x4[] _transforms = new Matrix4x4[ARRAY_LIMIT];
        private readonly Vector4[] _colors = new Vector4[ARRAY_LIMIT];

        public IndirectMeshInstancer(Mesh mesh, Material material, /* int instanceLimit, */ Bounds bounds,
            int layer = 0, ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
            : base(mesh, material, /* instanceLimit */ ARRAY_LIMIT, layer, shadowMode, receiveShadows, lightProbing, lightProxy)
        {
            _bounds = bounds;

            // _transforms = new(instanceLimit, GraphicsArray.INDIRECT_STRUCTURED);
            // _colors = new(instanceLimit, GraphicsArray.INDIRECT_STRUCTURED);
        }

        protected override void DisposeManaged()
        {
            // _transforms.Dispose();
            // _colors.Dispose();
        }

        protected override void OnEndDraw()
        {
            // _properties.SetBuffer(_transformsProperty, _transforms);
            // _properties.SetBuffer(_colorsProperty, _colors);
            _properties.SetMatrixArray(_transformsProperty, _transforms);
            _properties.SetVectorArray(_colorsProperty, _colors);

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
            _transforms[InstanceCount] = Matrix4x4.TRS(position, rotation, scale);
            _colors[InstanceCount] = color;
        }
    }
}