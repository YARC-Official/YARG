using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Game.Settings;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Customization;
using YARG.Settings.Types;

using SystemColor = System.Drawing.Color;
using UnityColor = UnityEngine.Color;

namespace YARG.Settings.Metadata
{
    public class PresetSubTab<T> : PresetSubTab where T : BasePreset
    {
        private struct FieldSettingInfo
        {
            public FieldInfo Field;

            public FieldInfo ParentField;

            public SettingType Type;
            public float Min;
            public float Max;

            public TType GetValue<TType>(object preset)
            {
                object value;
                if (ParentField is not null)
                {
                    var subSection = ParentField.GetValue(preset);
                    value = Field.GetValue(subSection);
                }
                else
                {
                    value = Field.GetValue(preset);
                }

                if (Field.FieldType.IsAssignableFrom(typeof(TType)))
                {
                    return (TType) value;
                }

                throw new Exception("Invalid type used for setting!");
            }

            public void SetValue<TType>(object preset, TType value)
            {
                object obj = preset;
                if (ParentField is not null)
                {
                    obj = ParentField.GetValue(preset);
                }

                if (Field.FieldType.IsAssignableFrom(typeof(TType)))
                {
                    Field.SetValue(obj, value);
                }
                else
                {
                    throw new Exception("Invalid type used for setting!");
                }
            }
        }

        // These are used in (almost) every engine preset and are a special setting type
        private static readonly List<FieldSettingInfo> _hitWindowFields = new();

        private readonly CustomContent<T> _customContent;
        public override CustomContent CustomContent => _customContent;

        private readonly bool _hasDescriptions;

        private T _presetRef;

        private readonly List<FieldSettingInfo> _fields = new();
        private readonly List<string> _subSections = new();

        private string _subSection;

        static PresetSubTab()
        {
            foreach (var field in typeof(EnginePreset.HitWindowPreset).GetFields())
            {
                ScanAndAddField(field, null, _hitWindowFields);
            }
        }

        public PresetSubTab(CustomContent<T> customContent, IPreviewBuilder previewBuilder, bool hasDescriptions)
            : base("Presets", "Generic", previewBuilder)
        {
            _customContent = customContent;
            _hasDescriptions = hasDescriptions;

            foreach (var field in typeof(T).GetFields())
            {
                var subSectionType = field.GetCustomAttribute<SettingSubSectionAttribute>();
                if (subSectionType is not null)
                {
                    foreach (var subField in field.FieldType.GetFields())
                    {
                        ScanAndAddField(subField, field, _fields);
                    }

                    _subSections.Add(field.Name);

                    continue;
                }

                ScanAndAddField(field, null, _fields);
            }
        }

        private static void ScanAndAddField(FieldInfo field, FieldInfo parentField, List<FieldSettingInfo> list)
        {
            // Since we don't wanna put attributes on each color within the color profile,
            // add a special case for that.
            if (field.FieldType == typeof(SystemColor) && typeof(T) == typeof(ColorProfile))
            {
                list.Add(new FieldSettingInfo
                {
                    ParentField = parentField,
                    Field = field,
                    Type = SettingType.Special
                });

                return;
            }

            var settingType = field.GetCustomAttribute<SettingTypeAttribute>();
            if (settingType is null)
            {
                return;
            }

            var range = field.GetCustomAttribute<SettingRangeAttribute>();

            list.Add(new FieldSettingInfo
            {
                ParentField = parentField,
                Field = field,

                Type = settingType.Type,
                Min = range?.Min ?? float.NegativeInfinity,
                Max = range?.Max ?? float.PositiveInfinity
            });
        }

        public override void SetPresetReference(object preset)
        {
            if (preset is not T t)
            {
                YargLogger.LogFormatError("Preset reference type `{0}` does not match `{1}`",
                    preset.GetType().Name, item2: typeof(T).Name);
                return;
            }

            _presetRef = t;
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            if (_subSections.Count > 0)
            {
                if (string.IsNullOrEmpty(_subSection))
                {
                    _subSection = _subSections[0];
                }

                var dropdown = new DropdownSetting<string>(_subSection, RefreshForSubSection);
                foreach (var subSection in _subSections)
                {
                    dropdown.Add(subSection);
                }

                CreateField(settingContainer, navGroup, typeof(T).Name, "SubSection", dropdown, false);
            }
            else
            {
                _subSection = null;
            }

            SpawnHeader(settingContainer, "PresetSettings");

            switch (_presetRef)
            {
                case ColorProfile:
                {
                    // Set the preview type
                    if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
                    {
                        trackPreviewBuilder.StartingGameMode = _subSection switch
                        {
                            nameof(ColorProfile.FiveFretGuitar) => GameMode.FiveFretGuitar,
                            nameof(ColorProfile.FourLaneDrums)  => GameMode.FourLaneDrums,
                            nameof(ColorProfile.FiveLaneDrums)  => GameMode.FiveLaneDrums,
                            nameof(ColorProfile.ProKeys)        => GameMode.ProKeys,
                            _                                   => throw new Exception("Unreachable.")
                        };
                    }
                    else
                    {
                        YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
                    }

                    goto default;
                }
                default:
                {
                    foreach (var field in _fields)
                    {
                        if (_subSection is not null && field.ParentField.Name != _subSection)
                        {
                            continue;
                        }

                        BuildField(field, settingContainer, navGroup, _presetRef);
                    }

                    break;
                }
            }
        }

        private void BuildField(FieldSettingInfo field, Transform container, NavigationGroup navGroup, T preset)
        {
            ISettingType setting = null;

            switch (field.Type)
            {
                case SettingType.Slider:
                {
                    setting = new SliderSetting(field.GetValue<float>(preset), field.Min, field.Max, (value) =>
                    {
                        field.SetValue(preset, value);
                    });

                    break;
                }
                case SettingType.MillisecondInput:
                {
                    setting = new DurationSetting(field.GetValue<double>(preset),
                        DurationInputField.Unit.Milliseconds, field.Max, (value) =>
                        {
                            field.SetValue(preset, value);
                        });

                    break;
                }
                case SettingType.Toggle:
                {
                    setting = new ToggleSetting(field.GetValue<bool>(preset), (value) =>
                    {
                        field.SetValue(preset, value);
                    });

                    break;
                }
                case SettingType.Special:
                {
                    // Keep the setting variable null because this method will deal with spawning itself
                    BuildSpecialSetting(container, navGroup, field, preset);

                    break;
                }
            }

            if (setting is not null)
            {
                CreateField(container, navGroup, typeof(T).Name, field.Field.Name, setting);
            }
        }

        private void BuildSpecialSetting(Transform container, NavigationGroup navGroup,
            FieldSettingInfo field, T preset)
        {
            if (field.Field.FieldType == typeof(SystemColor))
            {
                var color = field.GetValue<SystemColor>(preset).ToUnityColor();

                var setting = new ColorSetting(color, true, (value) =>
                {
                    field.SetValue(preset, value.ToSystemColor());
                });

                CreateField(container, navGroup, typeof(T).Name, field.Field.Name, setting);
            }
            else if (field.Field.FieldType == typeof(EnginePreset.HitWindowPreset))
            {
                var hitWindow = field.GetValue<EnginePreset.HitWindowPreset>(preset);

                // Create the important fields
                CreateFields(container, navGroup, typeof(T).Name, new()
                {
                    (
                        nameof(hitWindow.IsDynamic),
                        // The settings menu has to be refreshed so the hit window settings below updates
                        new ToggleSetting(hitWindow.IsDynamic, (value) =>
                        {
                            // If this gets called, it refreshes before it can update.
                            // We must update the dynamic hit window bool here.
                            hitWindow.IsDynamic = value;

                            SettingsMenu.Instance.RefreshAndKeepPosition();
                        })
                    ),
                    (
                        "HitWindow",
                        // Since the hit window setting is a reference type, we don't need a callback
                        new HitWindowSetting(hitWindow)
                    )
                });

                // Create the other fields
                foreach (var windowField in _hitWindowFields)
                {
                    // Every field should not be added if it is not a dynamic window (except for the ratio)
                    if (!hitWindow.IsDynamic &&
                        windowField.Field.Name != nameof(EnginePreset.HitWindowPreset.FrontToBackRatio))
                    {
                        continue;
                    }

                    if (windowField.Type != SettingType.Slider)
                    {
                        throw new Exception("Non-slider types are not supported within the hit window preset.");
                    }

                    var setting = new SliderSetting((float) windowField.GetValue<double>(hitWindow),
                        windowField.Min, windowField.Max, (value) =>
                        {
                            windowField.SetValue(hitWindow, (double) value);
                        });
                    CreateField(container, navGroup, typeof(T).Name, windowField.Field.Name, setting);
                }
            }
        }

        private void CreateField(Transform container, NavigationGroup navGroup, string presetName, string name,
            ISettingType settingType, bool hasDescription)
        {
            var visual = SpawnSettingVisual(settingType, container);
            visual.AssignPresetSetting($"{presetName}.{name}", hasDescription, settingType);
            navGroup.AddNavigatable(visual.gameObject);
        }

        private void CreateField(Transform container, NavigationGroup navGroup, string presetName, string name,
            ISettingType settingType)
        {
            CreateField(container, navGroup, presetName, name, settingType, _hasDescriptions);
        }

        private void CreateFields(Transform container, NavigationGroup navGroup, string presetName,
            List<(string Name, ISettingType SettingType)> settings)
        {
            foreach (var (name, setting) in settings)
            {
                CreateField(container, navGroup, presetName, name, setting);
            }
        }

        private void RefreshForSubSection(string subSection)
        {
            _subSection = subSection;
            SettingsMenu.Instance.Refresh();
        }
    }
}