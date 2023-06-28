using System.Collections.Generic;
using UnityEngine;

public class CameraPreviewTexture : MonoBehaviour
{
    public static RenderTexture PreviewTexture { get; private set; }

    private static List<CameraPreviewTexture> previews = new();

    private void Awake()
    {
        previews.Add(this);
    }

    private void OnDestroy()
    {
        previews.Remove(this);
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
            RenderTextureFormat.ARGBHalf
        );
        descriptor.mipCount = 0;
        PreviewTexture = new RenderTexture(descriptor);

        foreach (var script in previews)
        {
            script.UpdateRenderTexture();
        }
    }
}