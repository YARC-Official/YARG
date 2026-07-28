using UnityEngine;

namespace LibVLCSharp
{
    [RequireComponent(typeof(Renderer))]
    public class VLCDisplayMesh : MonoBehaviour
    {
        [SerializeField] private VLCVideoProviderBase mediaPlayer;

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
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(_texturePropertyId, texture);
            _renderer.SetPropertyBlock(_propBlock);
        }

        private void ClearTexture()
        {
            if (_renderer == null)
                return;

            _renderer.GetPropertyBlock(_propBlock);
            _propBlock.SetTexture(_texturePropertyId, Texture2D.blackTexture);
            _renderer.SetPropertyBlock(_propBlock);
        }
    }
}
