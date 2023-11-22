using System;
using System.Collections.Generic;
using System.IO;
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
        private static readonly GameObject _presetActions = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetActions")
            .WaitForCompletion();
        private static readonly GameObject _presetDefaultText = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/PresetDefaultText")
            .WaitForCompletion();

        // We essentially need to create sub-tabs for each preset
        private static readonly Dictionary<CustomContent, MetadataTab> _presetTabs = new()
        {
            {
                CustomContentManager.CameraSettings,
                new MetadataTab("Presets", previewBuilder: new TrackPreviewBuilder())
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
                new MetadataTab("Presets", previewBuilder: new TrackPreviewBuilder())
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

        private Guid _selectedPresetId;
        public BasePreset SelectedPreset
        {
            get => SelectedContent.GetBasePresetById(_selectedPresetId);
            set => _selectedPresetId = value.Id;
        }

        private FileSystemWatcher _watcher;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            SelectedContent = _presetTabs.Keys.First();
            ResetSelectedPreset();
        }

        public void ResetSelectedPreset()
        {
            SelectedPreset = SelectedContent.DefaultBasePresets[0];
        }

        public override void OnTabEnter()
        {
            _watcher = new FileSystemWatcher(CustomContentManager.CustomizationDirectory, "*.json")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            // This is async, so we must queue an action for the main thread
            _watcher.Changed += (_, args) =>
            {
                Debug.Log("Preset change detected!");

                // Queue the reload on the main thread
                UnityMainThreadCallback.QueueEvent(() =>
                {
                    OnPresetChanged(args.FullPath);
                });
            };
        }

        private static void OnPresetChanged(string path)
        {
            // Find which custom content container uses the directory of the preset
            foreach (var content in CustomContentManager.CustomContentContainers)
            {
                if (content.ContentDirectory != Directory.GetParent(path)?.FullName) continue;

                // Reload it
                content.ReloadPresetAtPath(path);

                // If the settings menu is open, and in the preset tab, also reload that
                if (SettingsMenu.Instance.gameObject.activeSelf &&
                    SettingsMenu.Instance.CurrentTab is PresetsTab)
                {
                    SettingsMenu.Instance.Refresh();
                }

                break;
            }
        }

        public override void OnTabExit()
        {
            _watcher?.Dispose();
            _watcher = null;
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            // Try to get the preset, and if unsuccessful, load the default one instead
            var preset = SelectedPreset;
            if (preset is null)
            {
                ResetSelectedPreset();
                preset = SelectedPreset;
            }

            SelectedContent.SetSettingsFromPreset(preset);

            // Create the preset type dropdown
            var typeDropdown = Object.Instantiate(_presetTypeDropdown, settingContainer);
            typeDropdown.GetComponent<PresetTypeDropdown>().Initialize(this, _presetTabs.Keys.ToArray());

            // Create the preset dropdown
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetDropdown>().Initialize(this);

            // Create the preset actions
            var actions = Object.Instantiate(_presetActions, settingContainer);
            actions.GetComponent<PresetActions>().Initialize(this);

            if (!_presetTabs.TryGetValue(SelectedContent, out var tab))
            {
                return;
            }

            if (preset is null || preset.DefaultPreset)
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