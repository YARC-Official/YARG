using System;
using UnityEngine;

namespace YARG.Venue
{
    public enum VenueLightLocation
    {
        Left,
        Right,
        Front,
        Back,
        Center,
        Crowd,
    }

    [RequireComponent(typeof(Light))]
    public class VenueLight : MonoBehaviour
    {
        private Light _light;

        [field: Header("This GameObject MUST be enabled by default!")]
        [field: SerializeField]
        public VenueLightLocation Location { get; private set; }

        private Quaternion _defaultRotation;
        private Color _defaultColor;
        private float _defaultIntensity;

        private void Start()
        {
            _light = GetComponent<Light>();

            _defaultRotation = transform.rotation;
            _defaultColor = _light.color;
            _defaultIntensity = _light.intensity;
        }

        private void Update()
        {
            var lightState = LightManager.Instance.MainLightState;

            _light.intensity = _defaultIntensity * lightState.Intensity;

            if (lightState.Color == null)
            {
                _light.color = _defaultColor;
            }
            else
            {
                _light.color = lightState.Color.Value;
            }
        }
    }
}