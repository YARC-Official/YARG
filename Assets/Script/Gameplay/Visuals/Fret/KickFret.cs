using UnityEngine;

namespace YARG.Gameplay.Visuals
{
    public class KickFret : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 50f;

        [SerializeField]
        private Animation _animation;
        [SerializeField]
        private MeshRenderer _kickFlashMesh;

        [Space]
        [SerializeField]
        private Texture2D[] _textures;

        private Material _material;
        private int _currentSprite;
        private float _updateTimer;

        private void Awake()
        {
            _material = _kickFlashMesh.material;

            _currentSprite = _textures.Length - 1;
            UpdateTexture();
        }

        public void Initialize(Color c)
        {
            _material.color = c;
        }

        private void UpdateTexture()
        {
            _material.mainTexture = _textures[_currentSprite];
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