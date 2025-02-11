using System.Collections.Generic;
using UnityEngine;

namespace YARG.Gameplay.Visuals
{

    public static class RenderingOrder
    {
        private static HashSet<string> _fixedMeshes = new();
        private static int _currentOffset = 10;
        private static readonly int _QueueOffset = Shader.PropertyToID("_QueueOffset");

        public static void FixUpMaterialRenderingOrder(MeshRenderer mesh)
        {
            if (!_fixedMeshes.Contains(mesh.name))
            {
                _fixedMeshes.Add(mesh.name);
                for (int i = 0; i < mesh.sharedMaterials.Length; ++i)
                {
                    var material = mesh.sharedMaterials[i];
                    if (material.GetFloat(_QueueOffset) == 0.0)
                    {
                        material.renderQueue = material.shader.renderQueue + _currentOffset;
                        material.SetFloat(_QueueOffset, _currentOffset);
                        _currentOffset++;
                    }
                }
            }
        }
    }
}
