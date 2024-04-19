using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.GraphicsTest.Instancing
{
    public class StandardMeshInstancer : MeshInstancer
    {
        /// <summary>
        /// The maximum number of instances that can be drawn by
        /// <see cref="Graphics.DrawMeshInstanced(Mesh, int, Material, Matrix4x4[])"/>.
        /// </summary>
        private const int INSTANCE_LIMIT = 1023;

        private static readonly int _colorProperty = Shader.PropertyToID("_Color");

        private readonly Matrix4x4[] _transforms = new Matrix4x4[INSTANCE_LIMIT];
        private readonly Vector4[] _colors = new Vector4[INSTANCE_LIMIT];

        public StandardMeshInstancer(Mesh mesh, Material material,
            int layer = 0, ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
            : base (mesh, material, INSTANCE_LIMIT, layer, shadowMode, receiveShadows, lightProbing, lightProxy)
        {
        }

        protected override void OnEndDraw()
        {
            _properties.SetVectorArray(_colorProperty, _colors);

            for (int subMesh = 0; subMesh < _mesh.subMeshCount; subMesh++)
            {
                Graphics.DrawMeshInstanced(
                    _mesh, subMesh, _material, _transforms, InstanceCount, _properties,
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