using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace LibVLCSharp
{
    [RequireComponent(typeof(RawImage))]
    public class VLCDisplayUGUI : MonoBehaviour
    {
        [FormerlySerializedAs("mediaPlayer")]
        [SerializeField] private VLCVideoProviderBase _mediaPlayer;

        public VLCVideoProviderBase MediaPlayer
        {
            get => _mediaPlayer;
            set
            {
                if (_mediaPlayer == value)
                    return;

                if (isActiveAndEnabled)
                    Unbind();

                _mediaPlayer = value;

                if (isActiveAndEnabled)
                    Bind();
            }
        }

        private RawImage _rawImage;
        private static readonly Rect UnflippedUVRect = new Rect(0, 0, 1, 1);
        private static readonly Rect VideoUVRect = new Rect(1, 1, -1, -1);

        private void Awake()
        {
            _rawImage = GetComponent<RawImage>();
        }

        private void OnEnable()
        {
            Bind();
        }

        private void OnDisable()
        {
            Unbind();
            ApplyTexture(null);
        }

        private void Bind()
        {
            if (_rawImage == null)
                return;

            if (_mediaPlayer != null)
                _mediaPlayer.OnTextureResized += ApplyTexture;

            ApplyTexture(_mediaPlayer != null ? _mediaPlayer.OutputTexture : null);
        }

        private void Unbind()
        {
            if (_mediaPlayer != null)
                _mediaPlayer.OnTextureResized -= ApplyTexture;
        }

        private void ApplyTexture(RenderTexture texture)
        {
            if (_rawImage == null)
                return;

            _rawImage.uvRect = texture != null ? VideoUVRect : UnflippedUVRect;
            _rawImage.texture = texture;
        }
    }
}
