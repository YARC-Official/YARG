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

        private Material _material;

        private void Start()
        {
            var rawImage = GetComponent<RawImage>();
            _material = rawImage.material;
        }

        private void Update()
        {
            var preset = PresetsTab.GetLastSelectedPreset(CustomContentManager.CameraSettings);
            _material.SetFloat(_curveFactor, preset.CurveFactor);
        }
    }
}