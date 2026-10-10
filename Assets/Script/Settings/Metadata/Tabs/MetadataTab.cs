using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.UI;
using YARG.Localization;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;

namespace YARG.Settings.Metadata
{
    public class MetadataTab : Tab, IEnumerable<AbstractMetadata>
    {
        // Prefabs needed for this tab type
        private static GameObject _buttonPrefab;
        private static GameObject _textPrefab;

        private Dictionary<string, BaseSettingVisual> _settingVisuals = new();
        private readonly List<AbstractMetadata> _settings = new();

        public IReadOnlyList<AbstractMetadata> Settings => _settings;
        public override IReadOnlyList<HeaderMetadata> Sections =>
            _settings.OfType<HeaderMetadata>().Where(header => header.IsVisible).ToArray();

        public override bool HasPreview
        {
            get
            {
                if (!base.HasPreview)
                {
                    return false;
                }

                var currentSection = SettingsMenu.Instance.CurrentSection;
                foreach (var metadata in _settings)
                {
                    if (metadata is not HeaderMetadata section)
                    {
                        continue;
                    }

                    if (!section.IsVisible || !section.ShowPreview)
                    {
                        continue;
                    }

                    if (section.HeaderName == currentSection)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public MetadataTab(string name, string icon = "Generic", IPreviewBuilder previewBuilder = null)
            : base(name, icon, previewBuilder)
        {
        }

        public override void BuildSettingTab(Transform container, NavigationGroup navGroup)
        {
            _settingVisuals.Clear();

            var currentSection = SettingsMenu.Instance.CurrentSection;
            var settingIndex = 0;

            // Once we've found the tab, add the settings
            foreach (var (settingMetadata, section) in VisibleSettings())
            {
                if (section != currentSection)
                {
                    continue;
                }

                switch (settingMetadata)
                {
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

                        var visual = SpawnSettingVisual(setting: setting, container: container);
                        visual.AssignSetting(field.FieldName, field.HasDescription);
                        if (field.RequiresRescan)
                        {
                            var notice = Localize.Key("Menu.Settings.RequiresRescan");
                            visual.SettingLabel.text += $"\n<size=70%><color=#829FAF>{notice}</color></size>";
                        }
                        visual.AssignIndex(settingIndex);
                        visual.SetEditable(setting.IsEditable);
                        ApplyRowLayout(visual);

                        _settingVisuals.Add(field.FieldName, visual);
                        navGroup.AddNavigatable(visual.gameObject);

                        settingIndex++;
                        break;
                    }
                }
            }
        }

        private static void ApplyRowLayout(BaseSettingVisual visual)
        {
            var row = (RectTransform) visual.transform;
            row.sizeDelta = new Vector2(x: row.sizeDelta.x, y: 84f);
            var controlFraction = visual is DMXChannelsSettingVisual ? 0.7f : 0.45f;
            var controls = (RectTransform) row.Find("Container");
            controls.anchorMin = new Vector2(x: 1f - controlFraction, y: 0f);
            controls.anchorMax = Vector2.one;
            controls.pivot = new Vector2(x: 1f, y: 0.5f);
            controls.anchoredPosition = new Vector2(x: -25f, y: 0f);
            controls.sizeDelta = new Vector2(x: -25f, y: 0f);

            var label = visual.SettingLabel;
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = new Vector2(x: 1f - controlFraction, y: 1f);
            label.rectTransform.offsetMin = new Vector2(x: 25f, y: 8f);
            label.rectTransform.offsetMax = new Vector2(x: -25f, y: -8f);
            label.enableAutoSizing = true;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.fontSizeMax = 24f;
            label.fontSizeMin = 18f;

            foreach (var slider in visual.GetComponentsInChildren<YARG.Menu.ValueSlider>())
            {
                var track = (RectTransform) slider.GetComponentInChildren<Slider>().transform;
                track.sizeDelta = new Vector2(x: -165f, y: track.sizeDelta.y);
                track.anchoredPosition = new Vector2(x: -82.5f, y: 0f);
                var value = slider.GetComponentInChildren<TMP_InputField>();
                var entry = (RectTransform) value.transform.parent;
                entry.anchorMin = new Vector2(x: 1f, y: 0f);
                entry.anchorMax = Vector2.one;
                entry.pivot = new Vector2(x: 1f, y: 0.5f);
                entry.anchoredPosition = Vector2.zero;
                entry.sizeDelta = new Vector2(x: 140f, y: 0f);
                value.textComponent.enableAutoSizing = true;
                value.textComponent.fontSizeMax = 28f;
                value.textComponent.fontSizeMin = 18f;
            }

            if (visual is DMXChannelsSettingVisual)
            {
                foreach (var input in visual.GetComponentsInChildren<TMP_InputField>())
                {
                    input.textViewport.offsetMin = new Vector2(x: 8f, y: 7f);
                    input.textViewport.offsetMax = new Vector2(x: -8f, y: -7f);
                    input.textComponent.enableAutoSizing = true;
                    input.textComponent.fontSizeMax = 24f;
                    input.textComponent.fontSizeMin = 18f;
                }
            }

            foreach (var dropdown in visual.GetComponentsInChildren<TMP_Dropdown>())
            {
                var caption = dropdown.captionText;
                caption.enableAutoSizing = true;
                caption.fontSizeMax = 28f;
                caption.fontSizeMin = 18f;
                caption.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private IEnumerable<(AbstractMetadata Metadata, string Section)> VisibleSettings()
        {
            var section = string.Empty;
            foreach (var metadata in _settings)
            {
                if (metadata is HeaderMetadata header)
                {
                    section = header.HeaderName;
                    continue;
                }

                if (metadata.IsVisible)
                {
                    yield return (metadata, section);
                }
            }
        }

        public (string Section, int Index) GetSettingPosition(string searchName)
        {
            var currentSection = string.Empty;
            var index = 0;
            foreach (var (metadata, section) in VisibleSettings())
            {
                if (section != currentSection)
                {
                    currentSection = section;
                    index = 0;
                }

                if (metadata is not FieldMetadata && metadata is not ButtonRowMetadata)
                {
                    continue;
                }

                if (metadata.UnlocalizedSearchNames.Contains(searchName))
                {
                    return (section, index);
                }

                index++;
            }

            throw new System.ArgumentException($"Setting '{searchName}' is not visible in tab '{Name}'.", nameof(searchName));
        }

        public override void OnSettingChanged()
        {
            foreach (var pair in _settingVisuals)
            {
                var setting = SettingsManager.GetSettingByName(pair.Key);
                pair.Value.SetEditable(setting.IsEditable);
                pair.Value.RefreshVisual();
            }
        }

        // For collection initializer support
        public void Add(AbstractMetadata setting) => _settings.Add(setting);
        private List<AbstractMetadata>.Enumerator GetEnumerator() => _settings.GetEnumerator();
        IEnumerator<AbstractMetadata> IEnumerable<AbstractMetadata>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
