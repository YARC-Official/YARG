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
        private static readonly GameObject _presetTypeDropdown = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetTypeDropdown")
            .WaitForCompletion();
        private static readonly GameObject _presetDropdown = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetDropdown")
            .WaitForCompletion();

        // We essentially need to create sub-tabs for each preset
        private static readonly Dictionary<CustomContent, MetadataTab> _presetTabs = new()
        {
            {
                CustomContentManager.CameraSettings,
                new MetadataTab("Presets")
                {
                    new HeaderMetadata("PresetSettings"),
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

        private CustomContent _selectedContent;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            _selectedContent = _presetTabs.Keys.First();
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            // Create the preset type dropdown
            var typeDropdown = Object.Instantiate(_presetTypeDropdown, settingContainer);
            typeDropdown.GetComponent<PresetTypeDropdown>().Initialize(_presetTabs.Keys.ToArray(), _selectedContent, t =>
            {
                _selectedContent = t;
                Refresh();
            });

            // Create the preset dropdown
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetDropdown>().Initialize(_selectedContent);

            if (!_presetTabs.TryGetValue(_selectedContent, out var tab))
            {
                return;
            }

            tab.BuildSettingTab(settingContainer, navGroup);
        }
    }
}