using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;
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
        private static readonly GameObject _presetDefaultText = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetDefaultText")
            .WaitForCompletion();

        // We essentially need to create sub-tabs for each preset
        private static readonly Dictionary<CustomContent, MetadataTab> _presetTabs = new()
        {
            {
                CustomContentManager.CameraSettings,
                new TrackPreviewTab("Presets")
                {
                    new HeaderMetadata("PresetSettings"),
                    "CameraPreset_FieldOfView",
                    "CameraPreset_PositionY",
                    "CameraPreset_PositionZ",
                    "CameraPreset_Rotation",
                    "CameraPreset_FadeLength",
                    "CameraPreset_CurveFactor"
                }
            },
            {
                CustomContentManager.ColorProfiles,
                new TrackPreviewTab("Presets")
                {
                    // TODO: Make a proper UI for this
                    new TextMetadata("ColorProfileSupport")
                }
            }
        };

        private CustomContent _selectedContent;
        public CustomContent SelectedContent
        {
            get => _selectedContent;
            set
            {
                _selectedContent = value;
                ResetSelectedPreset();
            }
        }

        public BasePreset SelectedPreset;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            SelectedContent = _presetTabs.Keys.First();
            ResetSelectedPreset();
        }

        public void ResetSelectedPreset()
        {
            SelectedPreset = SelectedContent.DefaultBasePresets[0];
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            SelectedContent.SetSettingsFromPreset(SelectedPreset);

            // Create the preset type dropdown
            var typeDropdown = Object.Instantiate(_presetTypeDropdown, settingContainer);
            typeDropdown.GetComponent<PresetTypeDropdown>().Initialize(this, _presetTabs.Keys.ToArray());

            // Create the preset dropdown
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetDropdown>().Initialize(this);

            if (!_presetTabs.TryGetValue(SelectedContent, out var tab))
            {
                return;
            }

            if (SelectedPreset is null || SelectedPreset.DefaultPreset)
            {
                Object.Instantiate(_presetDefaultText, settingContainer);
            }
            else
            {
                // Create the settings
                tab.BuildSettingTab(settingContainer, navGroup);
            }
        }

        public override async UniTask BuildPreviewWorld(Transform worldContainer)
        {
            if (!_presetTabs.TryGetValue(SelectedContent, out var tab))
            {
                return;
            }

            await tab.BuildPreviewWorld(worldContainer);
        }

        public override async UniTask BuildPreviewUI(Transform uiContainer)
        {
            if (!_presetTabs.TryGetValue(SelectedContent, out var tab))
            {
                return;
            }

            await tab.BuildPreviewUI(uiContainer);
        }

        public override void OnSettingChanged()
        {
            SelectedContent.SetPresetFromSettings(SelectedPreset);
        }
    }
}