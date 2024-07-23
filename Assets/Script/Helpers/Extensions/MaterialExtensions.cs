using UnityEngine;
using YARG.Rendering;

namespace YARG.Helpers.Extensions
{
    public static class MaterialExtensions
    {
        private static readonly int _fadeZeroPosition = Shader.PropertyToID("_FadeZeroPosition");
        private static readonly int _fadeFullPosition = Shader.PropertyToID("_FadeFullPosition");

        public static void SetFade(this Material material, float fadePos, float fadeSize)
        {
            material.SetVector(_fadeZeroPosition, new Vector4(0f, 0f, fadePos, 0f));
            material.SetVector(_fadeFullPosition, new Vector4(0f, 0f, fadePos - fadeSize, 0f));
        }

        public static void SetFade(this RendererPropertyWrapper properties, float fadePos, float fadeSize)
        {
            using (properties.SetMultiple())
            {
                properties.SetVector(_fadeZeroPosition, new Vector4(0f, 0f, fadePos, 0f));
                properties.SetVector(_fadeFullPosition, new Vector4(0f, 0f, fadePos - fadeSize, 0f));
            }
        }
    }
}