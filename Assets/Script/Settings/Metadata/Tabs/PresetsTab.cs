using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using Object = UnityEngine.Object;

namespace YARG.Settings.Metadata
{
    public class PresetsTab : Tab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _presetDropdown = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetTypeDropdown")
            .WaitForCompletion();

        // We essentially need to create sub-tabs for each preset
        private static readonly Dictionary<Type, MetadataTab> _presetTabs = new()
        {
            {
                typeof(CameraPreset),
                new MetadataTab("Presets")
                {
                    "CameraPreset_FieldOfView",
                    "CameraPreset_PositionY",
                    "CameraPreset_PositionZ",
                    "CameraPreset_Rotation",
                    "CameraPreset_FadeStart",
                    "CameraPreset_FadeLength",
                    "CameraPreset_CurveFactor"
                }
            }
        };

        private Type _selectedType;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            _selectedType = _presetTabs.Keys.First();
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetTypeDropdown>().Initialize(_presetTabs.Keys.ToArray(), _selectedType, t =>
            {
                _selectedType = t;
                Refresh();
            });

            if (!_presetTabs.TryGetValue(_selectedType, out var tab))
            {
                return;
            }

            tab.BuildSettingTab(settingContainer, navGroup);
        }
    }
}