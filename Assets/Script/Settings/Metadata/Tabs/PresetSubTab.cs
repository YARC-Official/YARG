using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Customization;
using YARG.Settings.Types;

namespace YARG.Settings.Metadata
{
    public abstract class PresetSubTab : Tab
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _headerPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Header")
            .WaitForCompletion();

        public abstract CustomContent CustomContent { get; }

        protected PresetSubTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public abstract void SetPresetReference(object preset);

        protected static void SpawnHeader(Transform container, string unlocalizedText)
        {
            // Spawn in the header
            var go = Object.Instantiate(_headerPrefab, container);

            // Set header text
            go.GetComponentInChildren<TextMeshProUGUI>().text =
                Localize.Key("Settings.Header", unlocalizedText);
        }
    }

    public partial class PresetSubTab<T> : PresetSubTab where T : BasePreset
    {
        private readonly CustomContent<T> _customContent;
        public override CustomContent CustomContent => _customContent;

        private T _presetRef;

        private readonly Dictionary<string, ISettingType> _settingFields = new();

        private string _subSection;

        public PresetSubTab(CustomContent<T> customContent, IPreviewBuilder previewBuilder = null)
            : base("Presets", "Generic", previewBuilder)
        {
            _customContent = customContent;
        }

        public override void SetPresetReference(object preset)
        {
            if (preset is not T t)
            {
                YargLogger.LogFormatError("Preset reference type `{0}` does not match `{1}`", preset.GetType().Name, item2: typeof(T).Name);
                return;
            }

            _presetRef = t;
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            _settingFields.Clear();

            switch (_presetRef)
            {
                case CameraPreset cameraPreset:
                    BuildForCamera(settingContainer, navGroup, cameraPreset);
                    break;
                case ColorProfile colorProfile:
                    BuildForColor(settingContainer, navGroup, colorProfile);
                    break;
                case EnginePreset enginePreset:
                    BuildForEngine(settingContainer, navGroup, enginePreset);
                    break;
                default:
                    YargLogger.LogFormatWarning("Setting tab not configured for preset type `{0}`!", typeof(T).Name);
                    break;
            }
        }

        public override void OnSettingChanged()
        {
            switch (_presetRef)
            {
                case CameraPreset cameraPreset:
                    UpdateForCamera(cameraPreset);
                    break;
                case ColorProfile colorProfile:
                    UpdateForColor(colorProfile);
                    break;
                case EnginePreset enginePreset:
                    UpdateForEngine(enginePreset);
                    break;
                default:
                    YargLogger.LogFormatWarning("Setting change not configured for preset type `{0}`!", typeof(T).Name);
                    break;
            }
        }

        private void CreateFields(Transform container, NavigationGroup navGroup, string presetName,
            List<(string Name, ISettingType SettingType)> settings)
        {
            foreach ((string name, var setting) in settings)
            {
                var visual = CreateField(container, presetName, name, setting);
                navGroup.AddNavigatable(visual.gameObject);
            }
        }

        private BaseSettingVisual CreateField(Transform container, string presetName, string name,
            ISettingType settingType)
        {
            var visual = SpawnSettingVisual(settingType, container);

            visual.AssignPresetSetting($"{presetName}.{name}", settingType);
            _settingFields.Add(name, settingType);

            return visual;
        }
    }
}