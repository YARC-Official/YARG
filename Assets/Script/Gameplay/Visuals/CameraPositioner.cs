using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Core.Game;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class CameraPositioner : MonoBehaviour
    {
        private const float BOUNCE_UNITS = 0.03f;
        private const float SPEED = 0.25f;

        private float _currentBounce;

        private void Start()
        {
            // Set anti-aliasing
            var info = GetComponent<UniversalAdditionalCameraData>();
            if (SettingsManager.Settings.LowQuality.Value)
            {
                info.antialiasing = AntialiasingMode.None;
            }
            else
            {
                info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                info.antialiasingQuality = AntialiasingQuality.Low;
            }
        }

        public void Initialize(CameraPreset preset, float fadePos, float fadeSize)
        {
            var camera = GetComponent<Camera>();
            // FOV
            camera.fieldOfView = preset.FieldOfView;

            // Position
            transform.localPosition = new Vector3(
                0f,
                preset.PositionY,
                preset.PositionZ - 6f);

            // Rotation
            transform.localRotation = Quaternion.Euler(preset.Rotation, 0f, 0f);
        }

        private void Update()
        {
            if (_currentBounce <= 0f) return;

            float speed = Time.deltaTime * SPEED;

            _currentBounce -= speed;
            transform.Translate(Vector3.up * speed, Space.World);
        }

        public void Bounce()
        {
            // Prevent bounce from stacking up
            transform.Translate(Vector3.up * _currentBounce, Space.World);

            // Remember that we should be moving in the opposite direction
            // because we're moving the camera, not the track.
            _currentBounce = BOUNCE_UNITS * SettingsManager.Settings.KickBounceMultiplier.Value;
            transform.Translate(Vector3.down * _currentBounce, Space.World);
        }
    }
}
