using UnityEngine;

namespace YARG.PlayMode
{
    public class KickFlashAnimation : MonoBehaviour
    {
        private const float SECONDS_PER_FRAME = 1f / 45f;

        [SerializeField]
        private Texture2D[] _textures;

        private Material _material;
        private int _currentSprite = 0;
        private float _updateTimer = 0f;

        private void Awake()
        {
            _material = GetComponent<MeshRenderer>().material;

            _currentSprite = _textures.Length - 1;
            UpdateTexture();
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

        public void PlayAnimation()
        {
            _updateTimer = 0f;
            _currentSprite = 0;
            UpdateTexture();
        }

        public void SetColor(Color c)
        {
            _material.color = c;
        }
    }
}