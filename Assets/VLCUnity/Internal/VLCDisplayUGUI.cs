using UnityEngine;
using UnityEngine.UI;

namespace LibVLCSharp
{
    [RequireComponent(typeof(RawImage))]
    public class VLCDisplayUGUI : MonoBehaviour
    {
        public VLCVideoProviderBase mediaPlayer;

        private RawImage _rawImage;
        private static readonly Rect UnflippedUVRect = new Rect(0, 0, 1, 1);
        private static readonly Rect VideoUVRect = new Rect(1, 1, -1, -1);

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            if (mediaPlayer != null)
                mediaPlayer.OnTextureResized += ApplyTexture;
        }

        private void OnDisable()
        {
            if (mediaPlayer != null)
                mediaPlayer.OnTextureResized -= ApplyTexture;

            ClearTexture();
        }

        private void ApplyTexture(RenderTexture texture)
        {
            _rawImage.uvRect = GetPlatformUVRect();
            _rawImage.texture = texture;
        }

        private void ClearTexture()
        {
            _rawImage.texture = null;
            _rawImage.uvRect = UnflippedUVRect;
        }

        private Rect GetPlatformUVRect()
        {
            return VideoUVRect;
        }
    }
}
