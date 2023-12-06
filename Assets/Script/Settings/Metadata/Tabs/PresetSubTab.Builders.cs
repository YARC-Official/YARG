using System;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Helpers.Extensions;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Types;

using SystemColor = System.Drawing.Color;
using UnityColor = UnityEngine.Color;

namespace YARG.Settings.Metadata
{
    public partial class PresetSubTab<T>
    {
        private const string CAMERA_PRESET = nameof(CameraPreset);
        private const string COLOR_PROFILE = nameof(ColorProfile);
        private const string ENGINE_PRESET = nameof(EnginePreset);

        private void BuildForCamera(Transform container,
            NavigationGroup navGroup, CameraPreset cameraPreset)
        {
            SpawnHeader(container, "PresetSettings");
            CreateFields(container, navGroup, CAMERA_PRESET, new()
            {
                (nameof(cameraPreset.FieldOfView), new SliderSetting(cameraPreset.FieldOfView,  40f, 150f)),
                (nameof(cameraPreset.PositionY),   new SliderSetting(cameraPreset.PositionY,    0f, 4f)),
                (nameof(cameraPreset.PositionZ),   new SliderSetting(cameraPreset.PositionZ,    0f, 12f)),
                (nameof(cameraPreset.Rotation),    new SliderSetting(cameraPreset.Rotation,     0f, 180f)),
                (nameof(cameraPreset.FadeLength),  new SliderSetting(cameraPreset.FadeLength,   0f, 5f)),
                (nameof(cameraPreset.CurveFactor), new SliderSetting(cameraPreset.CurveFactor, -3f, 3f)),
            });
        }

        private void UpdateForCamera(CameraPreset cameraPreset)
        {
            float Get(string name) => ((SliderSetting) _settingFields[name]).Value;

            cameraPreset.FieldOfView = Get(nameof(cameraPreset.FieldOfView));
            cameraPreset.PositionY   = Get(nameof(cameraPreset.PositionY));
            cameraPreset.PositionZ   = Get(nameof(cameraPreset.PositionZ));
            cameraPreset.Rotation    = Get(nameof(cameraPreset.Rotation));
            cameraPreset.FadeLength  = Get(nameof(cameraPreset.FadeLength));
            cameraPreset.CurveFactor = Get(nameof(cameraPreset.CurveFactor));
        }

        private void BuildForColor(Transform container,
            NavigationGroup navGroup, ColorProfile colorProfile)
        {
            // Set sub-section
            if (string.IsNullOrEmpty(_subSection))
            {
                _subSection = nameof(Instrument.FiveFretGuitar);
            }

            // Create instrument dropdown
            var dropdown = CreateField(container, COLOR_PROFILE, "Instrument", new DropdownSetting<string>(new()
            {
                nameof(Instrument.FiveFretGuitar),
                nameof(Instrument.FourLaneDrums),
                nameof(Instrument.FiveLaneDrums)
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
                if (field.FieldType != typeof(SystemColor)) continue;

                // Get the starting value
                var color = ((SystemColor) field.GetValue(instrumentProfile)).ToUnityColor();

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
                if (field.FieldType != typeof(SystemColor)) continue;

                // Get the setting
                var setting = _settingFields[field.Name];

                // Set value
                var color = ((UnityColor) setting.ValueAsObject).ToSystemColor();
                field.SetValue(instrumentProfile, color);
            }
        }

        private object GetSelectedInstrumentProfile(ColorProfile c)
        {
            return _subSection switch
            {
                nameof(Instrument.FiveFretGuitar) => c.FiveFretGuitar,
                nameof(Instrument.FourLaneDrums)  => c.FourLaneDrums,
                nameof(Instrument.FiveLaneDrums)  => c.FiveLaneDrums,
                _                => throw new Exception("Unreachable.")
            };
        }

        private void BuildForEngine(Transform container,
            NavigationGroup navGroup, EnginePreset enginePreset)
        {
            // Set sub-section
            if (string.IsNullOrEmpty(_subSection))
            {
                _subSection = nameof(EnginePreset.FiveFretGuitarPreset);
            }

            // Create game mode dropdown
            var dropdown = CreateField(container, ENGINE_PRESET, "GameMode", new DropdownSetting<string>(new()
            {
                nameof(EnginePreset.FiveFretGuitarPreset),
                nameof(EnginePreset.DrumsPreset)
            }, _subSection, (value) =>
            {
                _subSection = value;
                SettingsMenu.Instance.Refresh();
            }));
            navGroup.AddNavigatable(dropdown.gameObject);

            // Header
            SpawnHeader(container, "PresetSettings");
        }

        private void UpdateForEngine(EnginePreset enginePreset)
        {

        }
    }
}