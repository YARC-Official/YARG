using System;
using UnityEngine;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    public partial class PresetSubTab<T>
    {
        private const string CAMERA_PRESET = "CameraPreset";
        private const string COLOR_PROFILE = "ColorProfile";

        private void BuildForCamera(Transform container,
            NavigationGroup navGroup, CameraPreset cameraPreset)
        {
            SpawnHeader(container, "PresetSettings");
            CreateFields(container, navGroup, CAMERA_PRESET, new()
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
            float Get(string name) => ((SliderSetting) _settingFields[name]).Value;

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
            // Set sub-section
            if (string.IsNullOrEmpty(_subSection))
            {
                _subSection = "FiveFretGuitar";
            }

            // Create instrument dropdown
            var dropdown = CreateField(container, COLOR_PROFILE, "Instrument", new DropdownSetting(new()
            {
                "FiveFretGuitar",
                "FourLaneDrums",
                "FiveLaneDrums"
            }, _subSection, (value) =>
            {
                _subSection = value;
                SettingsMenu.Instance.Refresh();
            }));
            navGroup.AddNavigatable(dropdown.gameObject);

            // Header
            SpawnHeader(container, "PresetSettings");

            // Reflection is slow, however, it's more maintainable in this case
            var instrumentProfile = GetSelectedInstrumentProfile(colorProfile);
            foreach (var field in instrumentProfile.GetType().GetFields())
            {
                // Skip non-color fields
                if (field.FieldType != typeof(System.Drawing.Color)) continue;

                // Get the starting value
                var color = ((System.Drawing.Color) field.GetValue(instrumentProfile)).ToUnityColor();

                // Add field
                var visual = CreateField(container, COLOR_PROFILE, field.Name, new ColorSetting(color, true));
                navGroup.AddNavigatable(visual.gameObject);
            }
        }

        private void UpdateForColor(ColorProfile colorProfile)
        {
            // Reflection is slow, however, it's more maintainable in this case
            var instrumentProfile = GetSelectedInstrumentProfile(colorProfile);
            foreach (var field in instrumentProfile.GetType().GetFields())
            {
                // Skip non-color fields
                if (field.FieldType != typeof(System.Drawing.Color)) continue;

                // Get the setting
                var setting = _settingFields[field.Name];

                // Set value
                var color = ((Color) setting.ValueAsObject).ToSystemColor();
                field.SetValue(instrumentProfile, color);
            }
        }

        private object GetSelectedInstrumentProfile(ColorProfile c)
        {
            return _subSection switch
            {
                "FiveFretGuitar" => c.FiveFretGuitar,
                "FourLaneDrums"  => c.FourLaneDrums,
                "FiveLaneDrums"  => c.FiveLaneDrums,
                _                => throw new Exception("Unreachable.")
            };
        }
    }
}