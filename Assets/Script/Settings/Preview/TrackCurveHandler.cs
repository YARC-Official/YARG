using UnityEngine;
using UnityEngine.UI;
using YARG.Settings.Customization;
using YARG.Settings.Metadata;

namespace YARG.Settings.Preview
{
    [RequireComponent(typeof(RawImage))]
    public class TrackCurveHandler : MonoBehaviour
    {
        private static readonly int _curveFactor = Shader.PropertyToID("_CurveFactor");
        private static readonly int _fadeParams = Shader.PropertyToID("_FadeParams");

        private Material _material;

        public Vector2 FadeParams = new Vector2(0.9f,0.95f);

        private void Start()
        {
            var rawImage = GetComponent<RawImage>();

            // Make sure to clone since RawImages don't use instanced materials
            _material = new Material(rawImage.material);
            rawImage.material = _material;
        }

        private void Update()
        {
            var preset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            _material.SetFloat(_curveFactor, preset.CurveFactor);
            _material.SetVector(_fadeParams, FadeParams);
        }
    }
}
