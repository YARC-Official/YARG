using UnityEngine;

namespace YARG.Helpers.Authoring
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in theme creation.
    // Changing the serialized fields in this file will result in older themes
    // not working properly. Only change if you need to.

    [RequireComponent(typeof(Light))]
    public class EffectLight : MonoBehaviour
    {
        [Space]
        [SerializeField]
        private bool _allowColoring = true;

        [Space]
        [SerializeField]
        private bool _fadeMode;
        [SerializeField]
        private float _fadeOutRate;

        private Light _light;
        private float _initialIntensity;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _initialIntensity = _light.intensity;
            _light.intensity = 0f;
        }

        private void Update()
        {
            if (_fadeMode && _light.intensity > 0f)
            {
                _light.intensity -= Time.deltaTime * _fadeOutRate;
            }
        }

        public void SetColor(Color c)
        {
            if (!_allowColoring) return;

            _light.color = c;
        }

        public void Play()
        {
            _light.intensity = _initialIntensity;
        }

        public void Stop()
        {
            _light.intensity = 0f;
        }
    }
}