using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;
using YARG.Core.Logging;
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
        private static readonly List<PresetSubTab> _presetTabs = new()
        {
            new PresetSubTab<CameraPreset>(
                CustomContentManager.CameraSettings,
                new TrackPreviewBuilder(),
                true),

            new PresetSubTab<ColorProfile>(
                CustomContentManager.ColorProfiles,
                new TrackPreviewBuilder(),
                false),

            new PresetSubTab<EnginePreset>(
                CustomContentManager.EnginePresets,
                new TrackPreviewBuilder(forceShowHitWindow: true),
                true),
        };

        private static readonly Dictionary<Type, BasePreset> _lastSelectedPresetOfType = new();
        private static readonly List<string> _ignoredPathUpdates = new();

        private PresetSubTab CurrentSubTab => _presetTabs
            .FirstOrDefault(i => i.CustomContent == SelectedContent);

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
            set
            {
                _selectedPresetId = value.Id;
                _lastSelectedPresetOfType[value.GetType()] = value;
            }
        }

        private FileSystemWatcher _watcher;

        public PresetsTab(string name, string icon = "Generic") : base(name, icon)
        {
            SelectedContent = _presetTabs.First().CustomContent;
            ResetSelectedPreset();
        }

        public void ResetSelectedPreset()
        {
            // Get the preferred preset
            SelectedPreset = GetLastSelectedBasePreset(SelectedContent);
        }

        public override void OnTabEnter()
        {
            _ignoredPathUpdates.Clear();

            _watcher = new FileSystemWatcher(CustomContentManager.CustomizationDirectory, "*.json")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            // This is async, so we must queue an action for the main thread
            _watcher.Changed += (_, args) =>
            {
                YargLogger.LogDebug("Preset change detected!");

                // Queue the reload on the main thread
                UnityMainThreadCallback.QueueEvent(() =>
                {
                    OnPresetChanged(args.FullPath);
                });
            };
        }

        private void OnPresetChanged(string path)
        {
            if (_ignoredPathUpdates.Contains(path))
            {
                YargLogger.LogDebug("Ignored preset change.");
                _ignoredPathUpdates.Remove(path);
                return;
            }

            // Find which custom content container uses the directory of the preset
            foreach (var content in CustomContentManager.CustomContentContainers)
            {
                if (content.FullContentDirectory != Directory.GetParent(path)?.FullName) continue;

                // Reload it
                content.ReloadPresetAtPath(path);

                // If the settings menu is open, and in the preset tab, also reload that
                if (SettingsMenu.Instance.gameObject.activeSelf &&
                    SettingsMenu.Instance.CurrentTab is PresetsTab)
                {
                    if (File.Exists(path))
                    {
                        // Reload the selected preset, otherwise the _lastSelectedPresetOfType
                        // will be pointing to the old reference (before the change)
                        SelectedPreset = SelectedPreset;
                    }

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

            // Create the preset type dropdown
            var typeDropdown = Object.Instantiate(_presetTypeDropdown, settingContainer);
            typeDropdown.GetComponent<PresetTypeDropdown>().Initialize(this,
                _presetTabs.Select(i => i.CustomContent).ToArray());

            // Create the preset dropdown
            var dropdown = Object.Instantiate(_presetDropdown, settingContainer);
            dropdown.GetComponent<PresetDropdown>().Initialize(this);

            // Create the preset actions
            var actions = Object.Instantiate(_presetActions, settingContainer);
            actions.GetComponent<PresetActions>().Initialize(this);

            // Get the tab for the selected custom content type
            var tab = CurrentSubTab;
            if (tab is null) return;

            if (preset is null || preset.DefaultPreset)
            {
                Object.Instantiate(_presetDefaultText, settingContainer);
            }
            else
            {
                // Create the settings
                tab.SetPresetReference(SelectedPreset);
                tab.BuildSettingTab(settingContainer, navGroup);
            }
        }

        public override async UniTask BuildPreviewWorld(Transform worldContainer)
        {
            var tab = CurrentSubTab;
            if (tab is null) return;

            await tab.BuildPreviewWorld(worldContainer);
        }

        public override async UniTask BuildPreviewUI(Transform uiContainer)
        {
            var tab = CurrentSubTab;
            if (tab is null) return;

            await tab.BuildPreviewUI(uiContainer);
        }

        public override void OnSettingChanged()
        {
            CurrentSubTab?.OnSettingChanged();
        }

        private static BasePreset GetLastSelectedBasePreset(CustomContent customContent)
        {
            // We need to get the type of the preset, so without reflection, this is the easiest way
            var defaultPreset = customContent.DefaultBasePresets[0];
            var lastPreset = _lastSelectedPresetOfType.GetValueOrDefault(defaultPreset.GetType());

            if (lastPreset is null)
            {
                return defaultPreset;
            }

            if (!customContent.HasPresetId(lastPreset.Id))
            {
                // Prevent an unadded preset from becoming the last selected one
                _lastSelectedPresetOfType.Remove(defaultPreset.GetType());

                return defaultPreset;
            }

            return lastPreset;
        }

        public static T GetLastSelectedPreset<T>(CustomContent<T> customContent) where T : BasePreset
        {
            return (T) GetLastSelectedBasePreset(customContent);
        }

        public static void IgnorePathUpdate(string path)
        {
            _ignoredPathUpdates.Add(path);
        }
    }
}