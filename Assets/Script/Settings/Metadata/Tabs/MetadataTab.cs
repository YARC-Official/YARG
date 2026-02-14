using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;

namespace YARG.Settings.Metadata
{
    public class MetadataTab : Tab, IEnumerable<AbstractMetadata>
    {
        // Prefabs needed for this tab type
        private static GameObject _headerPrefab;
        private static GameObject _buttonPrefab;
        private static GameObject _textPrefab;

        private Dictionary<string, BaseSettingVisual> _settingVisuals = new();
        private readonly List<AbstractMetadata> _settings = new();

        public IReadOnlyList<AbstractMetadata> Settings => _settings;

        public MetadataTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public override void BuildSettingTab(Transform container, NavigationGroup navGroup)
        {
            _settingVisuals.Clear();
            var settingIndex = 0;

            // Once we've found the tab, add the settings
            foreach (var settingMetadata in _settings)
            {
                switch (settingMetadata)
                {
                    case HeaderMetadata header:
                    {
                        if (_headerPrefab == null)
                        {
                            _headerPrefab = Addressables
                                .LoadAssetAsync<GameObject>("SettingTab/Header")
                                .WaitForCompletion();
                        }
                        // Spawn in the header
                        var go = Object.Instantiate(_headerPrefab, container);

                        // Set header text
                        go.GetComponentInChildren<TextMeshProUGUI>().text =
                            Localize.Key("Settings.Header", header.HeaderName);

                        settingIndex = 0;
                        break;
                    }
                    case ButtonRowMetadata buttonRow:
                    {
                        if (_buttonPrefab == null)
                        {
                            _buttonPrefab = Addressables
                                .LoadAssetAsync<GameObject>("SettingTab/Button")
                                .WaitForCompletion();
                        }
                        // Spawn the button
                        var go = Object.Instantiate(_buttonPrefab, container);

                        var buttonGroup = go.GetComponent<SettingsButton>();
                        buttonGroup.SetInfo(buttonRow.Buttons);
                        navGroup.AddNavigatable(buttonGroup);

                        break;
                    }
                    case TextMetadata text:
                    {
                        if (_textPrefab == null)
                        {
                            _textPrefab = Addressables
                                .LoadAssetAsync<GameObject>("SettingTab/Text")
                                .WaitForCompletion();
                        }
                        // Spawn in the header
                        var go = Object.Instantiate(_textPrefab, container);

                        // Set text
                        go.GetComponentInChildren<TextMeshProUGUI>().text =
                            Localize.Key("Settings.Text", text.TextName);

                        break;
                    }
                    case FieldMetadata field:
                    {
                        var setting = SettingsManager.GetSettingByName(field.FieldName);

                        var visual = SpawnSettingVisual(setting, container);
                        visual.AssignSetting(field.FieldName, field.HasDescription);
                        visual.AssignIndex(settingIndex);

                        _settingVisuals.Add(field.FieldName, visual);
                        navGroup.AddNavigatable(visual.gameObject);

                        settingIndex++;
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