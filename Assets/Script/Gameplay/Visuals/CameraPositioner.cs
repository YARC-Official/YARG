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

        public void Initialize(CameraPreset preset)
        {
            // FOV
            GetComponent<Camera>().fieldOfView = preset.FieldOfView;

            // Position
            transform.localPosition = new Vector3(
                0f,
                preset.PositionY,
                preset.PositionZ - 6f);

            // Rotation
            transform.localRotation = Quaternion.Euler(preset.Rotation, 0f, 0f);
        }
    }
}