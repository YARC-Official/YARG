using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Venue
{
    // WARNING: Changing this could break themes or venues!
    //
    // This script is used a lot in venue creation.
    // Changing the serialized fields in this file will result in older venues
    // not working properly. Only change if you need to.

    public enum VenueLightLocation
    {
        Generic = 0,

        Left    = 1,
        Right   = 2,
        Front   = 3,
        Back    = 4,
        Center  = 5,
        Crowd   = 6
    }

    public enum VenueSpotLightLocation
    {
        None,
        Bass,
        Drums,
        Guitar,
        Vocals,
    }

    [RequireComponent(typeof(Light))]
    public class VenueLight : MonoBehaviour
    {

        [field: Header("This GameObject MUST be enabled by default!")]
        [field: SerializeField]
        public VenueLightLocation Location { get; private set; }

        [field: SerializeField]
        public bool IsSpotlight { get; private set; }
        [field: SerializeField]
        public VenueSpotLightLocation SpotlightLocation { get; private set; }

        [field: Header("Spotlight only")]
        [SerializeField]
        private bool _followCharacter;

        [SerializeField]
        private Transform _characterToFollow;

        private LightManager _lightManager;
        private Light _light;

        private Quaternion _defaultRotation;
        private Color _defaultColor;
        private float _defaultIntensity;

        private bool _previousSpotlightState = false;

        private void Start()
        {
            _lightManager = FindObjectOfType<LightManager>();
            _light = GetComponent<Light>();

            _defaultRotation = transform.rotation;
            _defaultColor = _light.color;
            _defaultIntensity = _light.intensity;
        }

        private void Update()
        {
            if (IsSpotlight)
            {
                UpdateSpotlight();
                return;
            }

            var lightState = _lightManager.GetLightStateFor(Location);

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

        private void UpdateSpotlight()
        {
            var lightState = _lightManager.GetSpotlightStateFor(SpotlightLocation);
            _light.intensity = lightState ? _defaultIntensity : 0f;
            if (lightState != _previousSpotlightState)
            {
                YargLogger.LogDebug($"Spotlight {SpotlightLocation} is {lightState}");
                _previousSpotlightState = lightState;
            }

            if (lightState && _followCharacter && _characterToFollow != null)
            {
                transform.LookAt(_characterToFollow);
            }
        }
    }
}