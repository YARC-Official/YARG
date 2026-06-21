using System.Collections.Generic;
using UnityEngine;

namespace YARG.Helpers
{
    public class CameraPreviewTexture : MonoBehaviour
    {
        public static RenderTexture PreviewTexture { get; private set; }

        private static readonly List<CameraPreviewTexture> _previews = new();

        private void Awake()
        {
            _previews.Add(this);
        }

        private void OnDestroy()
        {
            _previews.Remove(this);
        }

        private void UpdateRenderTexture()
        {
            GetComponent<Camera>().targetTexture = PreviewTexture;
        }

        public static void SetAllPreviews()
        {
            if (PreviewTexture != null)
            {
                PreviewTexture.Release();
            }

            // Create render texture
            var descriptor = new RenderTextureDescriptor(
                Screen.width, Screen.height,
                RenderTextureFormat.ARGBHalf,
                32
            );
            descriptor.mipCount = 0;
            PreviewTexture = new RenderTexture(descriptor);

            foreach (var script in _previews)
            {
                script.UpdateRenderTexture();
            }
        }
    }
}
