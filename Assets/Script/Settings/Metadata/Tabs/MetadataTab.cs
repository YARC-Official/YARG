using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Localization.Components;
using YARG.Helpers;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;

namespace YARG.Settings.Metadata
{
    public class MetadataTab : Tab, IEnumerable<AbstractMetadata>
    {
        // Prefabs needed for this tab type
        private static readonly GameObject _headerPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Header")
            .WaitForCompletion();
        private static readonly GameObject _buttonPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Button")
            .WaitForCompletion();
        private static readonly GameObject _textPrefab = Addressables
            .LoadAssetAsync<GameObject>("SettingTab/Text")
            .WaitForCompletion();

        private Dictionary<string, BaseSettingVisual> _settingVisuals = new();
        private readonly List<AbstractMetadata> _settings = new();

        public MetadataTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public override void BuildSettingTab(Transform container, NavigationGroup navGroup)
        {
            _settingVisuals.Clear();

            // Once we've found the tab, add the settings
            foreach (var settingMetadata in _settings)
            {
                switch (settingMetadata)
                {
                    case HeaderMetadata header:
                    {
                        // Spawn in the header
                        var go = Object.Instantiate(_headerPrefab, container);

                        // Set header text
                        go.GetComponentInChildren<LocalizeStringEvent>().StringReference =
                            LocaleHelper.StringReference("Settings", $"Header.{header.HeaderName}");

                        break;
                    }
                    case ButtonRowMetadata buttonRow:
                    {
                        // Spawn the button
                        var go = Object.Instantiate(_buttonPrefab, container);

                        var buttonGroup = go.GetComponent<SettingsButton>();
                        buttonGroup.SetInfo(Name, buttonRow.Buttons);
                        navGroup.AddNavigatable(buttonGroup);

                        break;
                    }
                    case TextMetadata text:
                    {
                        // Spawn in the header
                        var go = Object.Instantiate(_textPrefab, container);

                        // Set header text
                        go.GetComponentInChildren<LocalizeStringEvent>().StringReference =
                            LocaleHelper.StringReference("Settings", $"Text.{text.TextName}");

                        break;
                    }
                    case FieldMetadata field:
                    {
                        var setting = SettingsManager.GetSettingByName(field.FieldName);

                        var visual = SpawnSettingVisual(setting, container);
                        visual.AssignSetting(Name, field.FieldName);

                        _settingVisuals.Add(field.FieldName, visual);
                        navGroup.AddNavigatable(visual.gameObject);

                        break;
                    }
                }
            }
        }

        // For collection initializer support
        public void Add(AbstractMetadata setting) => _settings.Add(setting);
        private List<AbstractMetadata>.Enumerator GetEnumerator() => _settings.GetEnumerator();
        IEnumerator<AbstractMetadata> IEnumerable<AbstractMetadata>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}