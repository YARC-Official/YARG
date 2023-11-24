using System;
using UnityEngine;
using UnityEngine.Serialization;

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
        public enum Mode
        {
            Normal,
            FadeOut,
            Wavy
        }

        [Space]
        [SerializeField]
        private bool _allowColoring = true;

        [FormerlySerializedAs("_lightMode")]
        [Space]
        [SerializeField]
        private Mode _mode;

        [Space]
        [SerializeField]
        private float _fadeOutRate;

        private Light _light;

        private float _initialIntensity;
        private bool _playing;

        private void Awake()
        {
            _light = GetComponent<Light>();
            _initialIntensity = _light.intensity;
        }

        private void Start()
        {
            // Do this here instead of in Awake because otherwise the
            // _initialIntensity would be zero for duplicated prefabs.
            _light.intensity = 0f;
        }

        private void Update()
        {
            // FadeOut mode
            if (_mode == Mode.FadeOut && _light.intensity > 0f)
            {
                _light.intensity -= Time.deltaTime * _fadeOutRate;
            }

            // Wavy mode
            if (_mode == Mode.Wavy && _playing)
            {
                // TODO: Maybe allow customizing this?
                _light.intensity = _initialIntensity +
                    Mathf.Sin(Time.time * 30f) * 0.075f +
                    Mathf.Sin(Time.time * 40f) * 0.075f;
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
            _playing = true;
        }

        public void Stop()
        {
            if (_mode != Mode.FadeOut)
            {
                _light.intensity = 0f;
            }

            _playing = false;
        }
    }
}