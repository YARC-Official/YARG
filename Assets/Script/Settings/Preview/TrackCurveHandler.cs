using UnityEngine;
using UnityEngine.UI;

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
            _material.SetFloat(_curveFactor, SettingsManager.Settings.CameraPreset_CurveFactor.Data);
        }
    }
}