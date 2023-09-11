using UnityEngine;
using UnityEngine.Rendering.Universal;
using YARG.Core.Game;
using YARG.Settings;

namespace YARG.Gameplay.Visuals
{
    public class CameraPositioner : MonoBehaviour
    {
        private void Start()
        {
            UpdateAntiAliasing();
        }

        private void UpdateAntiAliasing()
        {
            // Set anti-aliasing
            var info = GetComponent<UniversalAdditionalCameraData>();
            if (SettingsManager.Settings.LowQuality.Data)
            {
                info.antialiasing = AntialiasingMode.None;
            }
            else
            {
                info.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                info.antialiasingQuality = AntialiasingQuality.Low;
            }
        }

        public void Initialize(CameraPreset preset)
        {
            Initialize(preset.FieldOfView, preset.PositionY, preset.PositionZ, preset.Rotation);
        }

        public void Initialize(float fov, float y, float z, float rot)
        {
            // FOV
            GetComponent<Camera>().fieldOfView = fov;

            // Position
            z -= 6f;
            transform.localPosition = new Vector3(0f, y, z);

            // Rotation
            transform.localRotation = Quaternion.Euler(rot, 0f, 0f);
        }
    }
}