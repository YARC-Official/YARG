using UnityEngine;
using UnityEngine.Serialization;

namespace YARG.Gameplay.Visuals
{
    public class KickFretFlash : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        // How much taller the flash bar gets at the instant of the hit, and how
        // long it takes to ease back down to its normal size.
        private const float POP_SCALE_MULTIPLIER = 1.4f;
        private const float POP_DURATION = 0.1f;

        [SerializeField]
        private MeshRenderer _kickFlashMesh;
        [FormerlySerializedAs("_textures")]
        [SerializeField]
        private Texture2D[] _kickFlashTextures;

        private Material _flashMaterial;
        private int _currentSprite;
        private float _updateTimer;

        private Transform _flashTransform;
        private Vector3 _baseScale;
        private float _popTimer;
        private bool _popping;

        private void Awake()
        {
            _flashMaterial = _kickFlashMesh.material;

            _currentSprite = _kickFlashTextures.Length - 1;
            UpdateTexture();

            _flashTransform = _kickFlashMesh.transform;
            _baseScale = _flashTransform.localScale;
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

            if (_popping)
            {
                UpdatePopScale();
            }
        }

        private void UpdatePopScale()
        {
            _popTimer += Time.deltaTime;

            if (_popTimer >= POP_DURATION)
            {
                _flashTransform.localScale = _baseScale;
                _popping = false;
                return;
            }

            // Pop in instantly (handled by PlayHitAnimation), then ease back down to the resting size
            float t = _popTimer / POP_DURATION;
            float ease = 1f - (1f - t) * (1f - t);
            float scale = Mathf.Lerp(POP_SCALE_MULTIPLIER, 1f, ease);

            _flashTransform.localScale = new Vector3(_baseScale.x, _baseScale.y * scale, _baseScale.z);
        }

        public void PlayHitAnimation()
        {
            _updateTimer = 0f;
            _currentSprite = 0;
            UpdateTexture();

            _popTimer = 0f;
            _popping = true;
            _flashTransform.localScale = new Vector3(_baseScale.x, _baseScale.y * POP_SCALE_MULTIPLIER, _baseScale.z);
        }
    }
}