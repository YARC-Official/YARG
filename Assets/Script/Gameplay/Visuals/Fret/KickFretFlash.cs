using UnityEngine;
using UnityEngine.Serialization;

namespace YARG.Gameplay.Visuals
{
    public class KickFretFlash : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        [SerializeField]
        private MeshRenderer _kickFlashMesh;
        [FormerlySerializedAs("_textures")]
        [SerializeField]
        private Texture2D[] _kickFlashTextures;

        private Material _flashMaterial;
        private int _currentSprite;
        private float _updateTimer;

        private void Awake()
        {
            _flashMaterial = _kickFlashMesh.material;

            _currentSprite = _kickFlashTextures.Length - 1;
            UpdateTexture();
        }

        public void Initialize(Color flash)
        {
            _flashMaterial.color = flash;
        }

        private void UpdateTexture()
        {
            _flashMaterial.mainTexture = _kickFlashTextures[_currentSprite];
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            while (_updateTimer >= SECONDS_PER_FRAME && _currentSprite < _kickFlashTextures.Length)
            {
                _updateTimer -= SECONDS_PER_FRAME;
                UpdateTexture();
                _currentSprite++;
            }
        }

        public void PlayHitAnimation(bool particles)
        {
            if (particles)
            {
                _updateTimer = 0f;
                _currentSprite = 0;
                UpdateTexture();
            }
        }
    }
}