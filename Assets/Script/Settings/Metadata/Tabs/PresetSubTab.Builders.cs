using UnityEngine;
using YARG.Core.Game;
using YARG.Menu.Navigation;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    public partial class PresetSubTab<T>
    {
        private void BuildForCamera(Transform container,
            NavigationGroup navGroup, CameraPreset cameraPreset)
        {
            SpawnHeader(container, "PresetSettings");
            CreateFields(container, navGroup, "CameraPreset", new()
            {
                ("FieldOfView", new SliderSetting(cameraPreset.FieldOfView,  40f, 150f)),
                ("PositionY",   new SliderSetting(cameraPreset.PositionY,    0f, 4f)),
                ("PositionZ",   new SliderSetting(cameraPreset.PositionZ,    0f, 12f)),
                ("Rotation",    new SliderSetting(cameraPreset.Rotation,     0f, 180f)),
                ("FadeLength",  new SliderSetting(cameraPreset.FadeLength,   0f, 5f)),
                ("CurveFactor", new SliderSetting(cameraPreset.CurveFactor, -3f, 3f)),
            });
        }

        private void UpdateForCamera(CameraPreset cameraPreset)
        {
            float Get(string name) => ((SliderSetting) _settingFields[name]).Data;

            cameraPreset.FieldOfView = Get("FieldOfView");
            cameraPreset.PositionY   = Get("PositionY");
            cameraPreset.PositionZ   = Get("PositionZ");
            cameraPreset.Rotation    = Get("Rotation");
            cameraPreset.FadeLength  = Get("FadeLength");
            cameraPreset.CurveFactor = Get("CurveFactor");
        }

        private void BuildForColor(Transform container,
            NavigationGroup navGroup, ColorProfile colorProfile)
        {
            SpawnHeader(container, "PresetSettings");
            // TODO
        }

        private void UpdateForColor(ColorProfile cameraPreset)
        {
            // TODO
        }
    }
}