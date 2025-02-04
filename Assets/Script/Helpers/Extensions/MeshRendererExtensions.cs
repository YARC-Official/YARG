using UnityEngine;
using YARG.Gameplay.Visuals;

namespace YARG.Helpers.Extensions
{
    public static class MeshRendererExtensions
    {
        private static readonly int _fadeZeroPosition = Shader.PropertyToID("_FadeZeroPosition");
        private static readonly int _fadeFullPosition = Shader.PropertyToID("_FadeFullPosition");

        public static void SetFade(this MeshRenderer mesh, float fadePos, float fadeSize)
        {
            for (int i = 0; i < mesh.sharedMaterials.Length; ++i)
            {
                mesh.GetPropertyBlock(MaterialPropertyInstance.Instance, i);
                MaterialPropertyInstance.Instance.SetVector(_fadeZeroPosition, new Vector4(0f, 0f, fadePos, 0f));
                MaterialPropertyInstance.Instance.SetVector(_fadeFullPosition, new Vector4(0f, 0f, fadePos - fadeSize, 0f));
                mesh.SetPropertyBlock(MaterialPropertyInstance.Instance, i);
            }
        }
    }
}
