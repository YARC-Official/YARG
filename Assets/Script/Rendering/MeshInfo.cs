using UnityEngine;

namespace YARG.Rendering
{
    public class MeshInfo
    {
        public readonly Mesh Mesh;

        public readonly Quaternion BaseRotation;
        public readonly Vector3 BaseScale;

        public MeshInfo(Mesh mesh, Quaternion baseRotation, Vector3 baseScale)
        {
            Mesh = mesh;

            BaseRotation = baseRotation;
            BaseScale = baseScale;
        }

        public MeshInfo(MeshFilter filter)
        {
            Mesh = filter.sharedMesh;

            var transform = filter.transform;
            BaseRotation = transform.rotation;
            BaseScale = transform.localScale;
        }
    }
}