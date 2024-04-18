using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Logging;

namespace YARG.GraphicsTest.Instancing
{
    public class MeshInstancer
    {
        /// <summary>
        /// The maximum number of instances that can be drawn by
        /// <see cref="Graphics.DrawMeshInstanced(Mesh, int, Material, Matrix4x4[])"/>.
        /// </summary>
        private const int INSTANCE_LIMIT = 1023;

        private static readonly int _colorProperty = Shader.PropertyToID("_Color");

        private readonly Mesh _mesh;
        private readonly Material _material;

        private readonly MaterialPropertyBlock _properties = new();

        private readonly Matrix4x4[] _transforms = new Matrix4x4[INSTANCE_LIMIT];
        private readonly Vector4[] _colors = new Vector4[INSTANCE_LIMIT];

        public int InstanceCount { get; private set; }
        public bool AtCapacity => InstanceCount >= INSTANCE_LIMIT;

        public MeshInstancer(Mesh mesh, Material material)
        {
            _mesh = mesh;
            _material = material;
        }

        public void BeginDraw()
        {
            InstanceCount = 0;
        }

        public void EndDraw(Camera camera, int layer = 0,
            ShadowCastingMode shadowMode = ShadowCastingMode.On, bool receiveShadows = true,
            LightProbeUsage lightProbing = LightProbeUsage.BlendProbes, LightProbeProxyVolume lightProxy = null)
        {
            if (InstanceCount < 1)
                return;

            _properties.SetVectorArray(_colorProperty, _colors);

            for (int subMesh = 0; subMesh < _mesh.subMeshCount; subMesh++)
            {
                Graphics.DrawMeshInstanced(
                    _mesh, subMesh, _material, _transforms, InstanceCount, _properties,
                    shadowMode, receiveShadows, layer, camera, lightProbing, lightProxy
                );
            }
        }

        public void RenderInstance(Vector3 position, Quaternion rotation, Vector3 scale, Color color)
        {
            if (InstanceCount >= INSTANCE_LIMIT)
            {
                YargLogger.LogDebug("Mesh instance limit reached!");
                return;
            }

            _transforms[InstanceCount] = Matrix4x4.TRS(position, rotation, scale);
            _colors[InstanceCount] = color;

            InstanceCount++;
        }
    }
}