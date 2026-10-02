using UnityEngine;
using UnityEngine.Serialization;

namespace LibVLCSharp
{
    [RequireComponent(typeof(Renderer))]
    public class VLCDisplayMesh : MonoBehaviour
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

        [Tooltip("Use _MainTex for Built-in shaders, or whatever your shader exposes.")]
        [SerializeField] private string textureProperty = "_MainTex";

        private Renderer _renderer;
        private MaterialPropertyBlock _propBlock;
        private int _texturePropertyId;

        private void Awake()
        {
            _renderer = GetComponent<Renderer>();
            _propBlock = new MaterialPropertyBlock();
            _texturePropertyId = Shader.PropertyToID(textureProperty);
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
            if (_renderer == null)
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
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(_texturePropertyId, texture != null ? (Texture)texture : Texture2D.blackTexture);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
