using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class KickFret : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        private static readonly int _emissionColor = Shader.PropertyToID("_EmissionColor");

        [SerializeField]
        private Animation _animation;
        [SerializeField]
        private MeshRenderer _kickFlashMesh;
        [SerializeField]
        private MeshRenderer _fretMesh;

        [Space]
        [SerializeField]
        private Texture2D[] _textures;

        private Material _flashMaterial;
        private int _currentSprite;
        private float _updateTimer;

        private void Awake()
        {
            _flashMaterial = _kickFlashMesh.material;

            _currentSprite = _textures.Length - 1;
            UpdateTexture();
        }

        public void Initialize(Color flash, Color fret, Color fretEmission)
        {
            _flashMaterial.color = flash;

            // The fret mesh's material does not need to be cached
            // because init is not called often.
            var fretMat = _fretMesh.material;
            fretMat.color = fret;
            fretMat.SetColor(_emissionColor, fretEmission);
        }

        private void UpdateTexture()
        {
            _flashMaterial.mainTexture = _textures[_currentSprite];
        }

        private void Update()
        {
            _updateTimer += Time.deltaTime;
            while (_updateTimer >= SECONDS_PER_FRAME && _currentSprite < _textures.Length)
            {
                _updateTimer -= SECONDS_PER_FRAME;
                UpdateTexture();
                _currentSprite++;
            }
        }

        public void PlayHitAnimation(bool particles)
        {
            StopAnimation();
            _animation.Play();

            if (particles)
            {
                _updateTimer = 0f;
                _currentSprite = 0;
                UpdateTexture();
            }
        }

        private void StopAnimation()
        {
            _animation.Stop();
            _animation.Rewind();
        }
    }
}