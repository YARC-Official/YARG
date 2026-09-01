using UnityEngine;

namespace YARG.Helpers.Extensions
{
    public static class RenderTextureExtensions
    {
        // A freshly created RenderTexture holds undefined GPU memory (often renders as grey)
        // until something first draws into it -- call this right after Create() for anything
        // that won't be drawn into immediately (e.g. video textures, before the first frame).
        public static void ClearToBlack(this RenderTexture texture)
        {
            var previousActive = RenderTexture.active;
            RenderTexture.active = texture;
            GL.Clear(false, true, Color.black);
            RenderTexture.active = previousActive;
        }
    }
}
