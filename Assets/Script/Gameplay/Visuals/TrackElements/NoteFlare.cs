using UnityEngine;
using UnityEngine.Rendering;
using YARG.Gameplay.Player;

namespace YARG.Gameplay.Visuals
{
    [RequireComponent(typeof(LensFlareComponentSRP))]
    public class NoteFlare : MonoBehaviour
    {
        public TrackPlayer TrackPlayer { get; set; }

        [SerializeField]
        private float _scaleConstant = 4.25f;

        private LensFlareComponentSRP _flare;

        private float _fadeFullPosition;
        private float _fadeZeroPosition;
        private float _fadeSize;

        private void Awake()
        {
            _flare = GetComponent<LensFlareComponentSRP>();
        }

        private void OnEnable()
        {
            _flare.intensity = 0f;
        }

        public void SetFade(float fadePos, float fadeSize)
        {
            _fadeFullPosition = fadePos - fadeSize;
            _fadeZeroPosition = fadePos;
            _fadeSize = fadeSize;
        }

        private void Update()
        {
            // Update flare intensity based on fade

            var pos = transform.position.z;
            float intensity = 1f;

            if (pos >= _fadeZeroPosition)
            {
                intensity = 0f;
            }
            else if (pos > _fadeFullPosition)
            {
                intensity = 1f - (pos - _fadeFullPosition) / _fadeSize;
            }

            _flare.intensity = intensity;

            // Update flare scale based on distance from camera

            _flare.scale = _scaleConstant /
                Vector3.Distance(transform.position, TrackPlayer.TrackCamera.transform.position);
        }
    }
}