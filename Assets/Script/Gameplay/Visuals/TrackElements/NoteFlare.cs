using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace YARG.Gameplay.Visuals
{
    [RequireComponent(typeof(LensFlareComponentSRP))]
    public class NoteFlare : MonoBehaviour
    {
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
        }
    }
}
