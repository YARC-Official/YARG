using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using YARG.Core;
using YARG.Core.Chart;
using YARG.Core.Game;
using YARG.Core.Game.Settings;
using YARG.Core.Input;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;
using YARG.Helpers.UI;
using YARG.Localization;
using YARG.Menu;
using YARG.Menu.Data;
using YARG.Menu.Dialogs;
using YARG.Menu.Navigation;
using YARG.Menu.Persistent;
using YARG.Menu.Settings;
using YARG.Menu.Settings.Visuals;
using YARG.Settings.Customization;
using YARG.Settings.Types;
using YARG.Themes;

using SystemColor = System.Drawing.Color;

// pattern: Imperative Shell

namespace YARG.Settings.Metadata
{
    public class PresetSubTab<T> : PresetSubTab where T : BasePreset
    {
        private int _fieldIndex;

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

        // Collapsible group state (for color profile fields)
        private readonly HashSet<string> _collapsedGroups = new();
        private bool _groupsInitialized;

        // Shared ColorSetting cache: maps "{subSection}.{fieldName}" → ColorSetting.
        // Ensures overlapping fields (if added in future views) share a single
        // instance so changing one visual updates siblings via RefreshVisual sync.
        // Cleared on each full rebuild in BuildGroupedFields / RefreshForSubSection.
        private readonly Dictionary<string, ColorSetting> _sharedColorSettings = new();

        // Captured nav-group index of the instrument-dropdown row, recorded at the
        // moment it is registered so ReselectInstrumentRow can target it directly
        // instead of guessing its position (Count - 2). -1 is the invalid sentinel
        // set at the start of each BuildSettingTab; a stale value never selects.
        private int _instrumentRowIndex = -1;

        static PresetSubTab()
        {
            foreach (var field in typeof(EnginePreset.HitWindowPreset).GetFields())
            {
                ScanAndAddField(field, null, _hitWindowFields);
            }
        }

        #region Collapsible color-field group definitions

        // When a ColorProfile sub-section has entries here, its fields are rendered
        // inside doubly-nested collapsible menus (Lane → [Notes, Fret, Activation])
        // instead of a flat list. Sub-sections without an entry fall through to flat
        // rendering.

        private readonly struct FieldGroup
        {
            public readonly string Name;
            public readonly bool CollapsedByDefault;
            public readonly FieldSubGroup[] SubGroups;

            public FieldGroup(string name, bool collapsedByDefault, FieldSubGroup[] subGroups)
            {
                Name = name;
                CollapsedByDefault = collapsedByDefault;
                SubGroups = subGroups;
            }
        }

        private readonly struct FieldSubGroup
        {
            public readonly string Name;
            public readonly bool OpenByDefault;
            public readonly string[] FieldNames;

            public FieldSubGroup(string name, bool openByDefault, params string[] fieldNames)
            {
                Name = name;
                OpenByDefault = openByDefault;
                FieldNames = fieldNames;
            }
        }

        // Sub-group name constants (null = no sub-header, fields render directly)
        private const string NotesSub = "Notes";
        private const string FretSub = "Fret";

        private static readonly Dictionary<string, FieldGroup[]> ColorFieldGroups = new()
        {
            [nameof(ColorProfile.FiveFretGuitar)] = BuildGuitarGroups(),
            [nameof(ColorProfile.FourLaneDrums)] = BuildDrumsGroups(cymbal: true),
            [nameof(ColorProfile.FiveLaneDrums)] = BuildDrumsGroups(cymbal: false),
        };

        private static FieldGroup[] BuildGuitarGroups()
        {
            // 5-fret guitar: Green, Red, Yellow, Blue, Orange, Open, General
            var lanes = new[] { "Green", "Red", "Yellow", "Blue", "Orange" };
            var groups = new List<FieldGroup>();

            foreach (var lane in lanes)
            {
                groups.Add(new FieldGroup(lane, collapsedByDefault: true, new[]
                {
                    new FieldSubGroup(NotesSub, openByDefault: true,
                        $"{lane}Note", $"{lane}NoteStarPower"),
                    new FieldSubGroup(FretSub, openByDefault: false,
                        $"{lane}Fret", $"{lane}FretInner", $"{lane}Particles"),
                }));
            }

            // Open notes
            groups.Add(new FieldGroup("Open", collapsedByDefault: true, new[]
            {
                new FieldSubGroup(NotesSub, openByDefault: true,
                    "OpenNote", "OpenNoteStarPower",
                    "OpenHopoNote", "OpenHopoNoteStarPower"),
                new FieldSubGroup(FretSub, openByDefault: false,
                    "OpenFret", "OpenFretInner", "OpenParticles"),
            }));

            // General
            groups.Add(new FieldGroup("General", collapsedByDefault: true, new[]
            {
                new FieldSubGroup(null, openByDefault: true,
                    "Metal", "MetalStarPower", "Miss", "TapStripEmission"),
            }));

            return groups.ToArray();
        }

        private static FieldGroup[] BuildDrumsGroups(bool cymbal)
        {
            // 4-lane: Red, Yellow, Blue, Green (+ cymbal variants)
            // 5-lane: Red, Yellow, Blue, Orange, Green (no cymbal variants)
            var lanes = cymbal
                ? new[] { "Red", "Yellow", "Blue", "Green" }
                : new[] { "Red", "Yellow", "Blue", "Orange", "Green" };

            var groups = new List<FieldGroup>();

            // Kick (includes DoubleKick). Activation notes are part of Notes
            // (they're variants of the same note, like starpower).
            groups.Add(new FieldGroup("Kick", collapsedByDefault: true, new[]
            {
                new FieldSubGroup(NotesSub, openByDefault: true,
                    "KickNote", "KickStarpower", "KickActivationNote",
                    "DoubleKickNote", "DoubleKickStarpower", "DoubleKickActivationNote"),
                new FieldSubGroup(FretSub, openByDefault: false,
                    "KickFret", "KickFretInner", "KickParticles",
                    "DoubleKickFret", "DoubleKickFretInner", "DoubleKickParticles"),
            }));

            foreach (var lane in lanes)
            {
                if (cymbal)
                {
                    groups.Add(new FieldGroup($"{lane} Lane", collapsedByDefault: true, new[]
                    {
                        new FieldSubGroup(NotesSub, openByDefault: true,
                            $"{lane}Drum", $"{lane}Cymbal",
                            $"{lane}DrumStarpower", $"{lane}CymbalStarpower",
                            $"{lane}PadActivationNote", $"{lane}CymbalActivationNote"),
                        new FieldSubGroup(FretSub, openByDefault: false,
                            $"{lane}Fret", $"{lane}FretInner", $"{lane}Particles",
                            $"{lane}CymbalFret", $"{lane}CymbalFretInner", $"{lane}CymbalParticles"),
                    }));
                }
                else
                {
                    groups.Add(new FieldGroup($"{lane} Lane", collapsedByDefault: true, new[]
                    {
                        new FieldSubGroup(NotesSub, openByDefault: true,
                            $"{lane}Note", $"{lane}Starpower", $"{lane}ActivationNote"),
                        new FieldSubGroup(FretSub, openByDefault: false,
                            $"{lane}Fret", $"{lane}FretInner", $"{lane}Particles"),
                    }));
                }
            }

            // General
            groups.Add(new FieldGroup("General", collapsedByDefault: true, new[]
            {
                new FieldSubGroup(null, openByDefault: true,
                    "Metal", "MetalStarPower", "Miss", "GhostStripEmission"),
            }));

            return groups.ToArray();
        }

        #endregion

        #region Instrument ↔ GameMode mapping

        // Single source of truth linking a ColorProfile sub-section (its identity
        // name), the preview GameMode it drives, and the human label shown in the
        // instrument dropdown. Adding an instrument (e.g. Pro Guitar, Elite Drums)
        // means adding one row here instead of touching several parallel switches.
        private static readonly (string SubSection, string Label, GameMode Mode)[] InstrumentModes =
        {
            (nameof(ColorProfile.FiveFretGuitar), "Five Fret Guitar", GameMode.FiveFretGuitar),
            (nameof(ColorProfile.FourLaneDrums),  "Four Lane Drums",  GameMode.FourLaneDrums),
            (nameof(ColorProfile.FiveLaneDrums),  "Five Lane Drums",  GameMode.FiveLaneDrums),
            (nameof(ColorProfile.ProKeys),        "Pro Keys",         GameMode.ProKeys),
        };

        private static bool TryGetModeForSubSection(string subSection, out GameMode mode)
        {
            foreach (var row in InstrumentModes)
            {
                if (row.SubSection == subSection)
                {
                    mode = row.Mode;
                    return true;
                }
            }

            mode = default;
            return false;
        }

        private static string SubSectionToLabel(string subSection)
        {
            foreach (var row in InstrumentModes)
            {
                if (row.SubSection == subSection)
                    return row.Label;
            }

            return null;
        }

        private static string ModeToSubSection(GameMode mode)
        {
            foreach (var row in InstrumentModes)
            {
                if (row.Mode == mode)
                    return row.SubSection;
            }

            return null;
        }

        #endregion

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
            var settingType = field.GetCustomAttribute<SettingTypeAttribute>();

            // But if the setting type attribute is set to ignore, respect that
            //
            // This exists because it can happen that a preset/other settings object
            // needs to have public fields that we don't want shown in the UI
            if (settingType is not null && settingType.Type == SettingType.Ignore)
            {
                return;
            }

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

            if (!ReferenceEquals(_presetRef, t))
            {
                // ColorSetting callbacks capture the preset they were created for.
                _sharedColorSettings.Clear();
            }

            _presetRef = t;
        }

        public override void BuildSettingTab(Transform settingContainer, NavigationGroup navGroup)
        {
            // Invalidate the captured instrument-row index so a stale value from a
            // prior build can never select a wrong row if preview controls aren't
            // rebuilt below. BuildPreviewControls repopulates it if it runs.
            _instrumentRowIndex = -1;

            // Drop stale swatch-tint closures on every rebuild — including
            // non-grouped sub-sections (e.g. Pro Keys) that bypass
            // BuildGroupedFields, so their closures can't outlive the Image
            // objects they captured from a previous section.
            _swatchTintRefreshers.Clear();

            // Sub-section init
            if (_subSections.Count > 0)
            {
                if (string.IsNullOrEmpty(_subSection))
                {
                    _subSection = _subSections[0];
                }
            }
            else
            {
                _subSection = null;
            }

            _fieldIndex = 0;

            // Sync shared preview state to this tab's TrackPreviewBuilder
            if (PreviewBuilder is TrackPreviewBuilder commonTpb)
            {
                commonTpb.StartingGameMode = PreviewOptions.GameMode;
                commonTpb.ForceStarPowerNotes = PreviewOptions.ForceStarPowerNotes;
                commonTpb.ForceStarPower = PreviewOptions.ForceStarPower;
                commonTpb.ForceGroove = PreviewOptions.ForceGroove;
                commonTpb.LeftyFlip = PreviewOptions.LeftyFlip;
            }

            switch (_presetRef)
            {
                case ColorProfile:
                {
                    if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
                    {
                        trackPreviewBuilder.StartingGameMode =
                            TryGetModeForSubSection(_subSection, out var subSectionMode)
                                ? subSectionMode
                                : throw new Exception("Unreachable.");
                    }
                    else
                    {
                        YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
                    }

                    goto default;
                }
                case HighwayPreset:
                {
                    goto default;
                }
                default:
                {
                    if (!HideFields)
                    {
                        if (_subSection is not null
                            && _presetRef is ColorProfile
                            && ColorFieldGroups.TryGetValue(_subSection, out var groups))
                        {
                            BuildGroupedFields(settingContainer, navGroup, _presetRef, groups);
                        }
                        else
                        {
                            foreach (var field in _fields)
                            {
                                if (_subSection is not null && field.ParentField.Name != _subSection)
                                {
                                    continue;
                                }

                                BuildField(field, settingContainer, navGroup, _presetRef);
                            }
                        }
                    }

                    break;
                }
            }

            // Build the instrument + preview options dropdowns in the preview
            // sidebar header. Built last so they sit at the END of the controller
            // nav order: navigating down past the last field reaches them, instead
            // of them lurking invisibly above the first field (which read as the
            // list "wrapping to nowhere").
            BuildPreviewControls(navGroup);
        }

        private const string PV_HEADER = "Preview Options";
        private const string PV_STAR_POWER_NOTES = "Star Power Notes";
        private const string PV_STAR_POWER_ACTIVE = "Star Power Active";
        private const string PV_GROOVE = "Groove";
        private const string PV_LEFTY_FLIP = "Lefty Flip";
        private const string PV_GLYPH_ON = "\u25C9 ";  // ◉ fisheye
        private const string PV_GLYPH_OFF = "\u25CB "; // ○ circle

        // Localization keys for the preview-toggle labels and caption. The PV_*
        // values above are the dropdown's internal option values (stable
        // sentinels for the OnChange switch); these keys map them to localized
        // display text via ValueToString.
        private const string PV_KEY_VISUALS = "Settings.PresetSetting.PreviewVisuals.Visuals.Name";
        private const string PV_KEY_STAR_POWER_NOTES = "Settings.PresetSetting.PreviewVisuals.StarPowerNotes.Name";
        private const string PV_KEY_STAR_POWER_ACTIVE = "Settings.PresetSetting.PreviewVisuals.StarPowerActive.Name";
        private const string PV_KEY_GROOVE = "Settings.PresetSetting.PreviewVisuals.Groove.Name";
        private const string PV_KEY_LEFTY_FLIP = "Settings.PresetSetting.PreviewVisuals.LeftyFlip.Name";

        /// <summary>
        /// Maps a color-group/sub-group name to its localized display string. The
        /// internal name (e.g. "Green", "Red Lane", "Kick") is kept as-is -- it is
        /// the collapse-state key and FieldSubGroup identity -- and only the text
        /// shown to the user is localized.
        /// </summary>
        private static string LocalizeGroupName(string name)
        {
            const string PREFIX = "Settings.PresetSetting.ColorGroups.";

            // Drum lanes are composite ("Red Lane"); format the suffix around the
            // color so translators can reorder words (e.g. French "Voie rouge").
            const string LANE_SUFFIX = " Lane";
            if (name.EndsWith(LANE_SUFFIX))
            {
                string color = name[..^LANE_SUFFIX.Length];
                return Localize.KeyFormat(PREFIX + "Lane", Localize.Key(PREFIX + color));
            }

            return Localize.Key(PREFIX + name);
        }

        /// <summary>
        /// Dropdown that acts as a menu of independent on/off toggles. Each item
        /// carries a state glyph regenerated by <see cref="ValueToString"/> on
        /// every visual refresh. The setting's value is the sentinel
        /// <see cref="PV_HEADER"/>, which is not in the option list — so the
        /// TMP_Dropdown sits at index -1 showing its placeholder as a fixed
        /// caption, and picking any item always changes the index (TMP suppresses
        /// onValueChanged when the index doesn't change). The OnChange handler in
        /// BuildPreviewControls snaps the value back to the sentinel after each
        /// pick.
        /// </summary>
        private class PreviewVisualsDropdownSetting : DropdownSetting<string>
        {
            public PreviewVisualsDropdownSetting() : base(PV_HEADER, null, localizable: false)
            {
            }

            public override string ValueToString(string value)
            {
                bool on;
                string key;
                switch (value)
                {
                    case PV_STAR_POWER_NOTES:
                        on = PreviewOptions.ForceStarPowerNotes;
                        key = PV_KEY_STAR_POWER_NOTES;
                        break;
                    case PV_STAR_POWER_ACTIVE:
                        on = PreviewOptions.ForceStarPower;
                        key = PV_KEY_STAR_POWER_ACTIVE;
                        break;
                    case PV_GROOVE:
                        on = PreviewOptions.ForceGroove;
                        key = PV_KEY_GROOVE;
                        break;
                    case PV_LEFTY_FLIP:
                        on = PreviewOptions.LeftyFlip;
                        key = PV_KEY_LEFTY_FLIP;
                        break;
                    default:
                        return value;
                }

                return (on ? PV_GLYPH_ON : PV_GLYPH_OFF) + Localize.Key(key);
            }
        }

        /// <summary>
        /// Instrument-selector dropdown. The value is the unlocalized label (kept
        /// stable for the OnChange match); the displayed text is the localized
        /// instrument name from Enum.Instrument.*, resolved through the sub-section
        /// name that backs each label.
        /// </summary>
        private class InstrumentDropdownSetting : DropdownSetting<string>
        {
            public InstrumentDropdownSetting(string currentLabel, Action<string> onChange)
                : base(currentLabel, onChange, localizable: false) { }

            public override string ValueToString(string value)
            {
                // value is the unlocalized label; find its sub-section so we can
                // resolve the matching Enum.Instrument localization key.
                foreach (var (subSection, label, _) in InstrumentModes)
                {
                    if (label == value)
                    {
                        return Localize.Key($"Enum.GameMode.{subSection}");
                    }
                }

                return value;
            }
        }

        /// <summary>
        /// Creates preview controls (instrument dropdown + preview visuals toggle
        /// dropdown, side by side) in the preview sidebar header area — the space
        /// above the highway preview where Setting Name / Setting Description text
        /// lives. Uses the existing setting prefabs via CreateField so fonts,
        /// layout, and sizing are correct.
        /// </summary>
        private void BuildPreviewControls(NavigationGroup navGroup)
        {
            if (PreviewBuilder is not TrackPreviewBuilder tpb) return;
            if (SettingsMenu.Instance?.PreviewContainerUI == null) return;

            // The preview container's parent is the Sidebar. The Header is a
            // child of the Sidebar that contains Setting Name / Description text.
            // We add our controls container as a child of the Sidebar, positioned
            // just below the Header and above the Preview Container.
            var sidebar = SettingsMenu.Instance.PreviewContainerUI.parent;
            if (sidebar == null) return;

            // Clear any previous controls (avoids duplicates on rebuild, and
            // removes the previous tab's dropdown when switching preset types)
            if (PreviewControlsContainer != null)
            {
                PreviewControlsContainer.DestroyChildren();
            }
            else
            {
                var go = new GameObject("PreviewControls");
                go.transform.SetParent(sidebar, false);
                var rect = go.AddComponent<RectTransform>();

                // The sidebar's Header (Setting Name/Description) is a fixed
                // 125px strip at the top. Anchor this row to the header's
                // bottom edge in pixels (not sidebar fractions, which land
                // inside the header at some resolutions), overlaying the top
                // of the preview area.
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.offsetMin = new Vector2(20f, -199f);
                rect.offsetMax = new Vector2(-20f, -135f);

                var layout = go.AddComponent<HorizontalLayoutGroup>();
                layout.spacing = 6;
                layout.childAlignment = TextAnchor.MiddleCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;

                PreviewControlsContainer = go.transform;
            }

            // --- Instrument selector dropdown ---
            // On Color Profile, this switches the sub-section (which colors are edited).
            // On other tabs, it only changes the preview instrument.
            string currentLabel = InstrumentModes[0].Label;
            foreach (var (_, label, mode) in InstrumentModes)
            {
                if (mode == PreviewOptions.GameMode)
                {
                    currentLabel = label;
                    break;
                }
            }

            // For Color Profile, sync game mode from sub-section
            if (typeof(T) == typeof(ColorProfile) && _subSection != null)
            {
                currentLabel = SubSectionToLabel(_subSection) ?? currentLabel;
            }

            var modeDropdown = new InstrumentDropdownSetting(currentLabel, selected =>
            {
                foreach (var (_, label, gameMode) in InstrumentModes)
                {
                    if (label == selected)
                    {
                        PreviewOptions.GameMode = gameMode;
                        tpb.StartingGameMode = gameMode;

                        if (typeof(T) == typeof(ColorProfile))
                        {
                            string newSub = ModeToSubSection(gameMode);
                            if (newSub != null && newSub != _subSection)
                            {
                                RefreshForSubSection(newSub);
                                ReselectInstrumentRow();
                                return;
                            }
                        }

                        SettingsMenu.Instance.Refresh();
                        ReselectInstrumentRow();
                        break;
                    }
                }
            });

            foreach (var (_, label, _) in InstrumentModes)
                modeDropdown.Add(label);

            var instrumentVisual = CreateField(PreviewControlsContainer, navGroup,
                "PreviewVisuals", "Instrument", modeDropdown, false);
            instrumentVisual?.HideLabel();
            UseDropdownListNavigation(instrumentVisual, navGroup);

            // Record the instrument row's index in the nav group right after it is
            // registered, so ReselectInstrumentRow can target it directly regardless
            // of how many navigatables follow it (e.g. the visuals dropdown).
            _instrumentRowIndex = navGroup.Count - 1;

            // --- Preview visuals toggle dropdown ---
            // Each item is an independent on/off toggle for the shared preview
            // state; the glyph in the label shows the current state.
            var visualsDropdown = new PreviewVisualsDropdownSetting
            {
                PV_STAR_POWER_NOTES,
                PV_STAR_POWER_ACTIVE,
                PV_GROOVE,
                PV_LEFTY_FLIP,
            };

            visualsDropdown.OnChange = selected =>
            {
                switch (selected)
                {
                    case PV_STAR_POWER_NOTES:
                        PreviewOptions.ForceStarPowerNotes = !PreviewOptions.ForceStarPowerNotes;
                        tpb.ForceStarPowerNotes = PreviewOptions.ForceStarPowerNotes;
                        break;
                    case PV_STAR_POWER_ACTIVE:
                        PreviewOptions.ForceStarPower = !PreviewOptions.ForceStarPower;
                        tpb.ForceStarPower = PreviewOptions.ForceStarPower;
                        break;
                    case PV_GROOVE:
                        PreviewOptions.ForceGroove = !PreviewOptions.ForceGroove;
                        tpb.ForceGroove = PreviewOptions.ForceGroove;
                        break;
                    case PV_LEFTY_FLIP:
                        PreviewOptions.LeftyFlip = !PreviewOptions.LeftyFlip;
                        tpb.LeftyFlip = PreviewOptions.LeftyFlip;
                        break;
                }

                // Snap back to the sentinel (index -1) so the placeholder caption
                // shows and re-picking the same item still fires onValueChanged.
                visualsDropdown.SetValueWithoutNotify(PV_HEADER);
            };

            var visualsVisual = CreateField(PreviewControlsContainer, navGroup, "PreviewVisuals", "Visuals",
                visualsDropdown, false);
            if (visualsVisual != null)
            {
                visualsVisual.HideLabel();

                // Fixed caption via TMP's placeholder, which is shown while the
                // dropdown's value is -1 (our sentinel value is not in the option
                // list, so the index is always -1 at rest).
                var tmpDropdown = visualsVisual.GetComponentInChildren<TMP_Dropdown>(true);
                if (tmpDropdown != null && tmpDropdown.captionText != null)
                {
                    var placeholder = UnityEngine.Object.Instantiate(
                        tmpDropdown.captionText, tmpDropdown.captionText.transform.parent);
                    placeholder.name = "Placeholder";
                    placeholder.text = Localize.Key(PV_KEY_VISUALS);
                    tmpDropdown.placeholder = placeholder;
                    tmpDropdown.SetValueWithoutNotify(-1);
                    tmpDropdown.RefreshShownValue();
                }
            }
            // After the placeholder setup so the selection highlight can tint it
            UseDropdownListNavigation(visualsVisual, navGroup);
        }

        /// <summary>
        /// Replaces a floating dropdown row's standard confirm-to-edit navigation
        /// with opening the actual TMP list under controller control (see
        /// <see cref="RuntimeNavigatable.OpenDropdownList"/>). The standard scheme
        /// edits an invisible list — no feedback — and any rebuild it triggers
        /// deselects the row and pops the scheme mid-edit.
        /// </summary>
        private static void UseDropdownListNavigation(BaseSettingVisual visual, NavigationGroup navGroup)
        {
            if (visual == null)
            {
                return;
            }
            var dropdown = visual.GetComponentInChildren<TMP_Dropdown>(true);
            if (dropdown == null)
            {
                return;
            }

            var standardNav = visual.GetComponent<BaseSettingNavigatable>();
            if (standardNav != null)
            {
                navGroup.RemoveNavigatable(standardNav);
                UnityEngine.Object.Destroy(standardNav);
            }

            // These rows have no row label (HideLabel) and their dropdown box
            // fills the row, so selection tints the visible caption text yellow
            // instead. Template item labels are deliberately excluded — TMP
            // clones them into the list when it opens.
            var captionTargets = new List<TMP_Text>();
            if (dropdown.captionText != null)
            {
                captionTargets.Add(dropdown.captionText);
            }
            if (dropdown.placeholder is TextMeshProUGUI placeholderText)
            {
                captionTargets.Add(placeholderText);
            }

            if (captionTargets.Count > 0)
            {
                var nav = RuntimeNavigatable.AttachTextHighlight(visual.gameObject,
                    () => RuntimeNavigatable.OpenDropdownList(dropdown), captionTargets.ToArray());
                navGroup.AddNavigatable(nav);

                // The authored dropdown background does not provide the desired
                // focused fill, so add a pure-color rounded overlay behind it.
                var overlay = new GameObject("DropdownFocusFill", typeof(RectTransform));
                var overlayRect = overlay.GetComponent<RectTransform>();
                overlayRect.SetParent(dropdown.transform, false);
                overlayRect.anchorMin = Vector2.zero;
                overlayRect.anchorMax = Vector2.one;
                overlayRect.offsetMin = new Vector2(3f, 3f);
                overlayRect.offsetMax = new Vector2(-3f, -3f);

                var overlayImage = overlay.AddComponent<Image>();
                overlayImage.sprite = SpriteHelper.GetRoundedRect(10);
                overlayImage.type = Image.Type.Sliced;
                overlayImage.color = new Color(0.1f, 0.1f, 0f, 1f); // #1a1a00
                overlayImage.raycastTarget = false;

                overlayRect.SetSiblingIndex(0);

                overlay.SetActive(false);
                nav.SelectionStateChanged += (_, selected, _) =>
                {
                    if (overlay != null) overlay.SetActive(selected);
                };
            }
            else
            {
                navGroup.AddNavigatable(RuntimeNavigatable.Attach(visual.gameObject,
                    () => RuntimeNavigatable.OpenDropdownList(dropdown)));
            }
        }

        /// <summary>
        /// Tints a setting row's label yellow while the row is selected
        /// (main-menu selection color), in addition to the row's own highlight.
        /// </summary>
        private static void AddRowLabelHighlight(NavigatableBehaviour nav, TextMeshProUGUI label)
        {
            if (nav == null || label == null)
            {
                return;
            }

            var defaultColor = label.color;
            nav.SelectionStateChanged += (_, selected, _) =>
            {
                if (label == null) return;

                var color = RuntimeNavigatable.SelectedTextColor;
                color.a = defaultColor.a;
                label.color = selected ? color : defaultColor;
            };
        }

        /// <summary>
        /// Replaces a color row's standard confirm-to-edit navigation (which
        /// pushes a scheme with nothing to edit) with opening the color picker,
        /// same as clicking the row's Color Picker button. Keeps the stock row
        /// highlight ("Selected Background").
        /// </summary>
        private static void UseColorPickerNavigation(BaseSettingVisual visual, NavigationGroup navGroup)
        {
            if (visual is not ColorSettingVisual colorVisual)
            {
                return;
            }

            var standardNav = visual.GetComponent<BaseSettingNavigatable>();
            if (standardNav != null)
            {
                navGroup.RemoveNavigatable(standardNav);
                UnityEngine.Object.Destroy(standardNav);
            }

            var nav = RuntimeNavigatable.Attach(visual.gameObject, colorVisual.OpenColorPicker);
            navGroup.AddNavigatable(nav);

            // Re-add the label highlight: the CreateField subscription died
            // with the standard navigatable
            AddRowLabelHighlight(nav, colorVisual.SettingLabel);

            // Outline the Color Picker button in yellow while the row has
            // focus — it's what confirm activates
            var pickerButton = visual.GetComponentInChildren<ColoredButton>(true);
            if (pickerButton != null)
            {
                AddFocusOutline(nav, RuntimeNavigatable.GetButtonOutlineTarget(pickerButton),
                    RuntimeNavigatable.SelectedTextColor);
            }
        }

        /// <summary>
        /// Adds a colored outline around <paramref name="target"/> that shows
        /// while <paramref name="nav"/> is selected, on top of the navigatable's
        /// existing selection visual.
        /// </summary>
        private static void AddFocusOutline(RuntimeNavigatable nav, Transform target, Color color)
        {
            var outline = RuntimeNavigatable.CreateSelectionOutline(target, color);
            var baseVisual = nav.SelectionVisual;
            nav.SelectionVisual = selected =>
            {
                baseVisual?.Invoke(selected);
                if (outline != null)
                {
                    outline.SetActive(selected);
                }
            };
        }

        /// <summary>
        /// After an instrument change rebuilds the whole tab, selection resets to
        /// the first row; put it back on the instrument dropdown so controller
        /// users aren't yanked to the top of the list.
        ///
        /// Targets the instrument row directly by its captured nav-group index
        /// (see <see cref="_instrumentRowIndex"/>) rather than guessing its
        /// position, so adding more preview navigatables can't silently redirect
        /// focus to the wrong row.
        /// </summary>
        private void ReselectInstrumentRow()
        {
            var navGroup = NavigationGroup.CurrentNavigationGroup;
            if (navGroup != null && _instrumentRowIndex >= 0 && _instrumentRowIndex < navGroup.Count)
            {
                navGroup.SelectAt(_instrumentRowIndex);
            }
        }

        private void BuildGroupedFields(Transform container, NavigationGroup navGroup,
            T preset, FieldGroup[] groups)
        {
            // Clear the shared color-setting cache on each full rebuild.
            _sharedColorSettings.Clear();

            // Initialize collapsed state on first render for this sub-section
            if (!_groupsInitialized)
            {
                _groupsInitialized = true;
                foreach (var group in groups)
                {
                    if (group.CollapsedByDefault)
                        _collapsedGroups.Add(group.Name);
                }
            }

            // Build a lookup of field name → FieldSettingInfo for the current sub-section,
            // and track ALL field names that belong to any group (so collapsed fields
            // don't fall through to the ungrouped fallback at the bottom).
            var fieldLookup = new Dictionary<string, FieldSettingInfo>();
            var groupedFieldNames = new HashSet<string>();
            foreach (var field in _fields)
            {
                if (_subSection is not null && field.ParentField.Name != _subSection)
                    continue;
                fieldLookup[field.Field.Name] = field;
            }
            foreach (var group in groups)
            {
                foreach (var subGroup in group.SubGroups)
                {
                    foreach (var fn in subGroup.FieldNames)
                        groupedFieldNames.Add(fn);
                }
            }

            foreach (var group in groups)
            {
                bool isCollapsed = _collapsedGroups.Contains(group.Name);

                // Spawn a clickable header with raw text (no localization key lookup).
                // Collapsed headers get rounded-rect swatches of the group's note
                // colors; expanded groups show the real color rows instead, and a
                // collapsed group's fields can't be edited, so the swatches can
                // never go stale between rebuilds.
                SpawnRawHeader(container, isCollapsed
                    ? $"\u25B6 {LocalizeGroupName(group.Name)}"
                    : $"\u25BC {LocalizeGroupName(group.Name)}");

                // Make the header clickable to toggle this group
                var headerGo = container.GetChild(container.childCount - 1).gameObject;

                if (isCollapsed)
                {
                    AddHeaderSwatches(headerGo, group, fieldLookup, preset);
                }

                if (headerGo.GetComponent<Button>() == null)
                    headerGo.AddComponent<Button>();
                var groupName = group.Name;

                void ToggleGroup()
                {
                    if (_collapsedGroups.Contains(groupName))
                        _collapsedGroups.Remove(groupName);
                    else
                        _collapsedGroups.Add(groupName);
                    SettingsMenu.Instance.RefreshSettingsKeepPosition();
                }

                headerGo.GetComponent<Button>().onClick.AddListener(ToggleGroup);

                // Controller navigation: the header joins the nav order at its
                // visual position and confirm toggles the group — without this,
                // the collapsed-by-default groups are unreachable without a mouse.
                // Selection tints the triangle+label yellow (main-menu style)
                // rather than drawing a highlight rectangle.
                navGroup.AddNavigatable(RuntimeNavigatable.AttachTextHighlight(headerGo, ToggleGroup));

                if (isCollapsed)
                    continue;

                // Render sub-groups and their fields
                foreach (var subGroup in group.SubGroups)
                {
                    if (subGroup.Name != null)
                    {
                        SpawnSubHeader(container, $"  {LocalizeGroupName(subGroup.Name)}");
                        _fieldIndex++;

                        if (subGroup.Name == FretSub)
                        {
                            var subHeaderGo = container.GetChild(container.childCount - 1).gameObject;
                            AddCopyFromNoteButton(subHeaderGo, group, subGroup, fieldLookup, preset, navGroup);
                        }
                    }

                    foreach (var fieldName in subGroup.FieldNames)
                    {
                        if (fieldLookup.TryGetValue(fieldName, out var field))
                        {
                            BuildField(field, container, navGroup, preset);
                        }
                    }
                }
            }

            // Render any remaining fields that aren't in any group (flat, at the bottom)
            foreach (var field in _fields)
            {
                if (_subSection is not null && field.ParentField.Name != _subSection)
                    continue;
                if (!groupedFieldNames.Contains(field.Field.Name))
                {
                    BuildField(field, container, navGroup, preset);
                }
            }
        }

        // Collapsed-header swatch layout. Swatches are 2:1 rounded rects anchored
        // at a fixed fraction of the header width so the runs line up across rows
        // (matching the old <pos=45%> glyph column).
        private const float SWATCH_ANCHOR_X = 0.45f;
        private const float SWATCH_WIDTH = 56f;
        private const float SWATCH_HEIGHT = 28f;
        private const float SWATCH_GAP = 10f;
        private const float SWATCH_BASE_GAP = 22f;

        /// <summary>
        /// Adds rounded-rect swatches to a collapsed group header: one rect per
        /// note variant in the group's Notes sub-group, filled with the note color
        /// and outlined with the instrument's Metal color (MetalStarPower for star
        /// power variants). Guitar/5-lane groups get two rects (regular + star
        /// power); 4-lane drum lanes get four (drum reg/SP, then cymbal reg/SP);
        /// the Kick group likewise pairs kick and double kick. Activation notes
        /// and the fret/particle colors are deliberately omitted, and the General
        /// group gets a single Miss swatch (its Metal colors are already visible
        /// as the strokes everywhere else).
        /// Alpha is deliberately dropped from both tints.
        /// </summary>
        private void AddHeaderSwatches(GameObject headerGo, FieldGroup group,
            Dictionary<string, FieldSettingInfo> fieldLookup, T preset)
        {
            if (group.Name == "General")
            {
                // One swatch for the missed-note color, with the regular Metal
                // stroke. Missed star power notes look like regular missed notes,
                // and there's no separate missed-cymbal color, so one rect covers
                // it. The Metal colors themselves need no swatches — they're
                // already visible as every other header's strokes.
                if (fieldLookup.TryGetValue("Miss", out var missField)
                    && missField.Field.FieldType == typeof(SystemColor))
                {
                    AddSwatchRect(headerGo.transform, 0f,
                        () => ToOpaqueUnityColor(missField.GetValue<SystemColor>(preset)),
                        () => DimStroke(ToOpaqueUnityColor(
                            GetFieldColor(fieldLookup, preset, "Metal", SystemColor.Gray))));
                }
                return;
            }

            // Collect the note-color fields from the first (Notes) sub-group,
            // skipping activation variants. Order: base variants in first-seen
            // order, regular before star power within each base.
            var entries = new List<(string BaseKey, bool StarPower, FieldSettingInfo Field)>();
            if (group.SubGroups.Length == 0)
            {
                return;
            }
            var subGroup = group.SubGroups[0];

            foreach (var fieldName in subGroup.FieldNames)
            {
                if (fieldName.Contains("Activation")
                    || !fieldLookup.TryGetValue(fieldName, out var field)
                    || field.Field.FieldType != typeof(SystemColor))
                {
                    continue;
                }

                // Pair regular + star power fields by a shared base key. The SP
                // field names aren't uniform: guitar keeps "Note" in the SP name
                // (GreenNote / GreenNoteStarPower) while 5-lane drums and kick drop
                // it (RedNote / RedStarpower, KickNote / KickStarpower) — so strip
                // a trailing "Note" too.
                string baseKey = fieldName
                    .Replace("StarPower", "")
                    .Replace("Starpower", "");
                bool starPower = baseKey != fieldName;
                if (baseKey.EndsWith("Note"))
                {
                    baseKey = baseKey.Substring(0, baseKey.Length - "Note".Length);
                }
                entries.Add((baseKey, starPower, field));
            }

            if (entries.Count == 0)
            {
                return;
            }

            var baseOrder = new List<string>();
            foreach (var entry in entries)
            {
                if (!baseOrder.Contains(entry.BaseKey))
                {
                    baseOrder.Add(entry.BaseKey);
                }
            }

            // Stroke colors come from the instrument-wide Metal fields, read
            // through delegates so a Metal edit re-tints the strokes live.
            SystemColor Metal() => GetFieldColor(fieldLookup, preset, "Metal", SystemColor.Gray);
            SystemColor MetalStarPower() => GetFieldColor(fieldLookup, preset, "MetalStarPower", Metal());

            float x = 0f;
            string previousBase = null;
            foreach (var baseKey in baseOrder)
            {
                foreach (var entry in entries)
                {
                    if (entry.BaseKey != baseKey)
                    {
                        continue;
                    }

                    if (previousBase != null)
                    {
                        x += previousBase == baseKey ? SWATCH_GAP : SWATCH_BASE_GAP;
                    }
                    previousBase = baseKey;

                    var fillField = entry.Field;
                    bool starPower = entry.StarPower;
                    AddSwatchRect(headerGo.transform, x,
                        () => ToOpaqueUnityColor(fillField.GetValue<SystemColor>(preset)),
                        () => DimStroke(ToOpaqueUnityColor(starPower ? MetalStarPower() : Metal())));
                    x += SWATCH_WIDTH;
                }
            }
        }

        private static SystemColor GetFieldColor(Dictionary<string, FieldSettingInfo> fieldLookup,
            T preset, string fieldName, SystemColor fallback)
        {
            if (fieldLookup.TryGetValue(fieldName, out var field)
                && field.Field.FieldType == typeof(SystemColor))
            {
                return field.GetValue<SystemColor>(preset);
            }
            return fallback;
        }

        private static Color ToOpaqueUnityColor(SystemColor color)
        {
            return new Color32(color.R, color.G, color.B, 255);
        }

        // In-game the Metal colors multiply a base texture that isn't pure white,
        // so a white Metal setting renders as silver. Dim the raw setting value to
        // approximate how the stroke color actually looks on the live preview.
        private const float SWATCH_STROKE_DIM = 0.75f;

        private static Color DimStroke(Color color)
        {
            return new Color(
                color.r * SWATCH_STROKE_DIM,
                color.g * SWATCH_STROKE_DIM,
                color.b * SWATCH_STROKE_DIM,
                color.a);
        }

        // Brand orange (Xanthous, #FFB636) — signals the mildly
        // destructive nature of the bulk copy (it overwrites every fret/particle
        // color in the lane).
        private static readonly Color COPY_BUTTON_ORANGE = new Color32(0xFF, 0xB6, 0x36, 0xFF);

        /// <summary>
        /// Adds a "Copy from note" shortcut button to a lane's Fret sub-header.
        /// It copies the lane's base note color (first field of the Notes
        /// sub-group — Note/Drum/KickNote per instrument) onto every fret and
        /// particle field in the sub-group, behind a confirmation dialog since it
        /// overwrites all of them. Kick's double-kick fret fields copy from
        /// DoubleKickNote instead, so the two kick variants stay independent.
        /// </summary>
        private void AddCopyFromNoteButton(GameObject subHeaderGo, FieldGroup group,
            FieldSubGroup fretSub, Dictionary<string, FieldSettingInfo> fieldLookup, T preset,
            NavigationGroup navGroup)
        {
            // The base note is the first color field of the Notes sub-group.
            if (group.SubGroups.Length == 0 || group.SubGroups[0].FieldNames.Length == 0)
            {
                return;
            }
            string baseFieldName = group.SubGroups[0].FieldNames[0];
            if (!fieldLookup.TryGetValue(baseFieldName, out var baseField)
                || baseField.Field.FieldType != typeof(SystemColor))
            {
                return;
            }

            // Grab the sub-header's own label before the button adds its own TMP
            var subHeaderLabel = subHeaderGo.GetComponentInChildren<TextMeshProUGUI>();

            var buttonGo = UnityEngine.Object.Instantiate(
                GetSmallRoundButtonPrefab(), subHeaderGo.transform);
            var rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            // Match the "Color Picker" buttons in the rows below: those get 175
            // wide from a LayoutElement in the ColorSetting row (not the prefab's
            // native 150), and the rows' right edge sits ~24.75 units inside the
            // header's (screenshot-measured to sub-pixel), so offset to line the
            // right edges up.
            rect.sizeDelta = new Vector2(175f, 30f);
            rect.anchoredPosition = new Vector2(-24.75f, 0f);

            var button = buttonGo.GetComponent<ColoredButton>();
            button.Text.text = Localize.Key("Settings.PresetSetting.Dialog.CopyFromNote.Button");
            button.SetBackgroundAndTextColor(COPY_BUTTON_ORANGE);

            void OpenConfirmDialog()
            {
                void Apply()
                {
                    ApplyBaseNoteColorToFretFields(baseField, fretSub, fieldLookup, preset);
                    DialogManager.Instance.ClearDialog();

                    // Rebuild so the visible color rows re-read the preset values
                    // (the shared ColorSetting cache is cleared on rebuild).
                    SettingsMenu.Instance.RefreshSettingsKeepPosition();
                }

                ShowCompactConfirmation(
                    Localize.Key("Settings.PresetSetting.Dialog.CopyFromNote.Title"),
                    Localize.Key("Settings.PresetSetting.Dialog.CopyFromNote.Message"),
                    "Menu.Common.Apply", MenuData.Colors.ConfirmButton, Apply);
            }

            button.OnClick.AddListener(OpenConfirmDialog);

            // Controller navigation: the Fret sub-header row is navigable and
            // confirm opens the copy dialog (same as clicking the button).
            // Selection tints only the sub-header's own label yellow — not the
            // button's text, which must stay readable on the orange background —
            // and outlines the button in the same yellow as the color rows'
            // picker-button outline.
            var copyNav = RuntimeNavigatable.AttachTextHighlight(
                subHeaderGo, OpenConfirmDialog, subHeaderLabel);
            AddFocusOutline(copyNav, RuntimeNavigatable.GetButtonOutlineTarget(button),
                RuntimeNavigatable.SelectedTextColor);
            navGroup.AddNavigatable(copyNav);
        }

        private void ApplyBaseNoteColorToFretFields(FieldSettingInfo baseField,
            FieldSubGroup fretSub, Dictionary<string, FieldSettingInfo> fieldLookup, T preset)
        {
            var baseColor = baseField.GetValue<SystemColor>(preset);

            SystemColor doubleKickColor = baseColor;
            bool hasDoubleKick = fieldLookup.TryGetValue("DoubleKickNote", out var doubleKickField)
                && doubleKickField.Field.FieldType == typeof(SystemColor);
            if (hasDoubleKick)
            {
                doubleKickColor = doubleKickField.GetValue<SystemColor>(preset);
            }

            foreach (var fieldName in fretSub.FieldNames)
            {
                if (!fieldLookup.TryGetValue(fieldName, out var field)
                    || field.Field.FieldType != typeof(SystemColor))
                {
                    continue;
                }

                var color = hasDoubleKick && fieldName.StartsWith("DoubleKick")
                    ? doubleKickColor
                    : baseColor;
                field.SetValue(preset, color);
            }
        }

        /// <summary>
        /// Creates one swatch: a fill Image with a stroke overlay Image stacked on
        /// top, parented to the header. Pure anchored children — the settings list's
        /// layout group only manages the header root, so these don't disturb layout.
        /// Raycasts are disabled so the header's collapse Button still gets clicks.
        /// </summary>
        private void AddSwatchRect(Transform header, float xOffset, Func<Color> fill, Func<Color> stroke)
        {
            var go = new GameObject("Swatch", typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(header, false);
            rect.anchorMin = new Vector2(SWATCH_ANCHOR_X, 0.5f);
            rect.anchorMax = new Vector2(SWATCH_ANCHOR_X, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(SWATCH_WIDTH, SWATCH_HEIGHT);
            rect.anchoredPosition = new Vector2(xOffset, 0f);

            var fillImage = go.AddComponent<Image>();
            fillImage.sprite = GetSwatchSprite(stroke: false);
            fillImage.raycastTarget = false;

            var strokeGo = new GameObject("Stroke", typeof(RectTransform));
            var strokeRect = strokeGo.GetComponent<RectTransform>();
            strokeRect.SetParent(rect, false);
            strokeRect.anchorMin = Vector2.zero;
            strokeRect.anchorMax = Vector2.one;
            strokeRect.offsetMin = Vector2.zero;
            strokeRect.offsetMax = Vector2.zero;

            var strokeImage = strokeGo.AddComponent<Image>();
            strokeImage.sprite = GetSwatchSprite(stroke: true);
            strokeImage.raycastTarget = false;

            // Colors are pulled through delegates so the swatch can re-read the
            // preset when a color changes (e.g. Metal edited while other groups
            // sit collapsed) instead of waiting for the next rebuild.
            void Refresh()
            {
                if (fillImage != null) fillImage.color = fill();
                if (strokeImage != null) strokeImage.color = stroke();
            }

            Refresh();
            _swatchTintRefreshers.Add(Refresh);
        }

        // Live swatch re-tint hooks, run after every color-setting change and
        // cleared on rebuild (the swatches are recreated with the headers).
        private readonly List<Action> _swatchTintRefreshers = new();

        private void RefreshHeaderSwatches()
        {
            foreach (var refresh in _swatchTintRefreshers)
            {
                refresh();
            }
        }

        // Procedurally generated white rounded-rect sprites (fill + inset stroke
        // ring), tinted via Image.color. Generated once at 2x display size for
        // antialiasing; cached for the lifetime of the app.
        private static Sprite _swatchFillSprite;
        private static Sprite _swatchStrokeSprite;

        private static Sprite GetSwatchSprite(bool stroke)
        {
            var cached = stroke ? _swatchStrokeSprite : _swatchFillSprite;
            if (cached != null)
            {
                return cached;
            }

            const int width = 112;   // 2x SWATCH_WIDTH
            const int height = 56;   // 2x SWATCH_HEIGHT
            const float radius = 14f;
            const float strokeWidth = 6f;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[width * height];
            var halfSize = new Vector2(width / 2f, height / 2f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Signed distance to the rounded rect (negative inside)
                    var p = new Vector2(x + 0.5f - halfSize.x, y + 0.5f - halfSize.y);
                    var q = new Vector2(
                        Mathf.Abs(p.x) - (halfSize.x - radius),
                        Mathf.Abs(p.y) - (halfSize.y - radius));
                    float distance =
                        new Vector2(Mathf.Max(q.x, 0f), Mathf.Max(q.y, 0f)).magnitude
                        + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - radius;

                    // ~1px antialiased coverage at the outer edge, and (for the
                    // stroke ring) at an inner edge strokeWidth further in.
                    float outer = Mathf.Clamp01(0.5f - distance);
                    float alpha = stroke
                        ? outer - Mathf.Clamp01(0.5f - (distance + strokeWidth))
                        : outer;

                    pixels[y * width + x] = new Color32(255, 255, 255,
                        (byte) Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            if (stroke)
            {
                _swatchStrokeSprite = sprite;
            }
            else
            {
                _swatchFillSprite = sprite;
            }
            return sprite;
        }

        private void BuildField(FieldSettingInfo field, Transform container, NavigationGroup navGroup, T preset)
        {
            // These legacy key colors belong to the deferred five-lane-keys
            // editor. Pro Keys uses the White/BlackNote and Overlay fields instead.
            if (_subSection == nameof(ColorProfile.ProKeys)
                && field.Field.Name is "WhiteKey" or "RedKey" or "YellowKey" or "BlueKey"
                    or "GreenKey" or "OrangeKey")
            {
                return;
            }

            ISettingType setting = null;

            switch (field.Type)
            {
                case SettingType.Slider:
                {
                    setting = new SliderSetting(field.GetValue<float>(preset), field.Min, field.Max, (value) =>
                    {
                        field.SetValue(preset, value);
                        // Re-trigger spotlight so preview updates for strip
                        // emission settings (no-op for unrelated sliders)
                        SpotlightFieldLane(field.Field.Name);
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
                case SettingType.FileInfo:
                {
                    var settingName = field.Field.Name;
                    setting = new FileInfoSetting(field.GetValue<FileInfo>(preset), preset, settingName, (value) =>
                    {
                        field.SetValue(preset, value);
                    });
                    break;
                }
            }

            if (setting is not null)
            {
                CreateField(container, navGroup, typeof(T).Name, field.Field.Name, setting);
            }
        }

        /// <summary>
        /// Spotlights the preview lane for a color-profile field: the next
        /// <see cref="Preview.FakeTrackPlayer.SPOTLIGHT_NOTE_COUNT"/> preview notes all
        /// come from that lane (cymbal vs. drum variant per the field, star power
        /// colors for star power fields). Does nothing for non-lane fields
        /// (Metal, Miss) and for double kick, which has no renderable form in the
        /// fake preview (it needs the dedicated-lane layout).
        /// </summary>
        private void SpotlightFieldLane(string fieldName)
        {
            if (_presetRef is not ColorProfile || _subSection is null) return;
            if (PreviewBuilder is not TrackPreviewBuilder tpb) return;

            if (fieldName.StartsWith("DoubleKick")) return;

            // Note-type spotlights for strip emission settings — show all taps
            // or all ghosts so the user can see the emission effect.
            if (fieldName == "TapStripEmission")
            {
                tpb.SpotlightNoteType(ThemeNoteType.Tap);
                return;
            }
            if (fieldName == "GhostStripEmission")
            {
                tpb.SpotlightNoteType(ThemeNoteType.Ghost);
                return;
            }
            if (fieldName == "OpenHopoNote")
            {
                tpb.SpotlightNoteType(ThemeNoteType.OpenHOPO, starPower: false);
                return;
            }
            if (fieldName == "OpenHopoNoteStarPower")
            {
                tpb.SpotlightNoteType(ThemeNoteType.OpenHOPO, starPower: true);
                return;
            }
            if (fieldName == "Miss")
            {
                tpb.SpotlightMiss();
                return;
            }
            if (fieldName == "MetalStarPower")
            {
                tpb.SpotlightStarPower();
                return;
            }

            bool starPower = fieldName.Contains("StarPower") || fieldName.Contains("Starpower");
            bool cymbal = fieldName.Contains("Cymbal");

            if (_subSection == nameof(ColorProfile.ProKeys)
                && fieldName is "WhiteNote" or "BlackNote" or "WhiteNoteStarPower" or "BlackNoteStarPower")
            {
                tpb.SpotlightProKeysNoteType(fieldName.StartsWith("Black", StringComparison.Ordinal), starPower);
                return;
            }

            switch (_subSection)
            {
                case nameof(ColorProfile.FiveFretGuitar):
                {
                    if (fieldName.StartsWith("Open"))
                    {
                        tpb.SpotlightLane((int) FiveFretGuitarFret.Open, centerNote: true, cymbal: false, starPower);
                        return;
                    }

                    int fret = LanePrefixToFret(fieldName, "Green", "Red", "Yellow", "Blue", "Orange");
                    if (fret > 0)
                    {
                        tpb.SpotlightLane(fret, centerNote: false, cymbal: false, starPower);
                    }
                    return;
                }
                case nameof(ColorProfile.FourLaneDrums):
                {
                    if (fieldName.StartsWith("Kick"))
                    {
                        tpb.SpotlightLane(0, centerNote: true, cymbal: false, starPower);
                        return;
                    }

                    int fret = LanePrefixToFret(fieldName, "Red", "Yellow", "Blue", "Green");
                    if (fret > 0)
                    {
                        tpb.SpotlightLane(fret, centerNote: false, cymbal, starPower);
                    }
                    return;
                }
                case nameof(ColorProfile.FiveLaneDrums):
                {
                    if (fieldName.StartsWith("Kick"))
                    {
                        tpb.SpotlightLane(0, centerNote: true, cymbal: false, starPower);
                        return;
                    }

                    // Yellow (2) and Orange (4) are the cymbal lanes in 5-lane
                    int fret = LanePrefixToFret(fieldName, "Red", "Yellow", "Blue", "Orange", "Green");
                    if (fret > 0)
                    {
                        tpb.SpotlightLane(fret, centerNote: false, cymbal: fret is 2 or 4, starPower);
                    }
                    return;
                }
            }
        }

        private static int LanePrefixToFret(string fieldName, params string[] lanes)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (fieldName.StartsWith(lanes[i], StringComparison.Ordinal))
                {
                    return i + 1;
                }
            }

            return -1;
        }

        public override void OnSettingSelected(string unlocalizedName)
        {
            // Names arrive as "{presetTypeName}.{fieldName}"; ignore anything
            // that isn't one of this preset type's fields (e.g. the preview
            // header dropdowns, which use the "PreviewVisuals" prefix).
            int dot = unlocalizedName.IndexOf('.');
            if (dot < 0 || unlocalizedName[..dot] != typeof(T).Name) return;

            SpotlightFieldLane(unlocalizedName[(dot + 1)..]);
        }

        /// <summary>
        /// Returns a shared <see cref="ColorSetting"/> for the given field. When
        /// the same logical field appears in multiple groups (future overlapping
        /// views), all visuals receive the same instance so sibling visuals stay
        /// in sync via the SettingChanged → RefreshVisual chain.
        /// </summary>
        private ColorSetting GetOrCreateColorSetting(FieldSettingInfo field, T preset)
        {
            string key = $"{_subSection}.{field.Field.Name}";

            if (_sharedColorSettings.TryGetValue(key, out var cached))
            {
                return cached;
            }

            var color = field.GetValue<SystemColor>(preset).ToUnityColor();

            // Alpha only has a visible effect on highway colors — note/fret
            // colors ignore it in-game — so the color profile page hides the
            // opacity field and the picker's A row.
            bool allowTransparency = typeof(T) != typeof(ColorProfile);

            string fieldName = field.Field.Name;
            var setting = new ColorSetting(color, allowTransparency, (value) =>
            {
                field.SetValue(preset, value.ToSystemColor());

                // Keep collapsed headers' swatches in sync (Metal edits re-tint
                // every stroke; a rebuild only happens on expand/collapse)
                RefreshHeaderSwatches();

                // Re-spotlight the lane so the user sees the new color applied
                SpotlightFieldLane(fieldName);
            });

            _sharedColorSettings[key] = setting;
            return setting;
        }

        private void BuildSpecialSetting(Transform container, NavigationGroup navGroup,
            FieldSettingInfo field, T preset)
        {
            if (field.Field.FieldType == typeof(SystemColor))
            {
                var setting = GetOrCreateColorSetting(field, preset);

                var visual = CreateField(container, navGroup, typeof(T).Name, field.Field.Name,
                    setting, _hasDescriptions);

                // Confirm on a color row opens the color picker (the standard
                // confirm-to-edit scheme has nothing to edit on these rows)
                UseColorPickerNavigation(visual, navGroup);
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
                    if (!hitWindow.IsDynamic)
                    {
                        bool dynamicOnlyField;
                        switch(windowField.Field.Name)
                        {
                            case nameof(EnginePreset.HitWindowPreset.FrontToBackRatio):
                            case nameof(EnginePreset.HitWindowPreset.LaneAutohitWindow):
                            case nameof(EnginePreset.HitWindowPreset.LaneProximityProtectionWindow):
                                dynamicOnlyField = false;
                                break;

                            default:
                                dynamicOnlyField = true;
                                break;
                        }

                        if (dynamicOnlyField)
                        {
                            continue;
                        }
                    }

                    ISettingType setting = null;

                    switch (windowField.Type)
                    {
                        case SettingType.Slider:
                            setting = new SliderSetting((float) windowField.GetValue<double>(hitWindow),
                                windowField.Min, windowField.Max, (value) =>
                                {
                                    windowField.SetValue(hitWindow, (double) value);
                                });
                            break;
                        case SettingType.MillisecondInput:
                            setting = new DurationSetting(windowField.GetValue<double>(hitWindow),
                                DurationInputField.Unit.Milliseconds, windowField.Max, (value) =>
                                {
                                    windowField.SetValue(hitWindow, value);
                                });
                            break;
                        default:
                            throw new Exception("Unsupported setting type in hit window preset.");
                    }

                    CreateField(container, navGroup, typeof(T).Name, windowField.Field.Name, setting);
                }
            }
        }

        private BaseSettingVisual CreateField(Transform container, NavigationGroup navGroup, string presetName, string name,
            ISettingType settingType, bool hasDescription)
        {
            var visual = SpawnSettingVisual(settingType, container);
            visual.AssignPresetSetting($"{presetName}.{name}", hasDescription, settingType);
            visual.AssignIndex(_fieldIndex);
            navGroup.AddNavigatable(visual.gameObject);
            AddRowLabelHighlight(visual.GetComponent<NavigatableBehaviour>(), visual.SettingLabel);
            _fieldIndex++;
            return visual;
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
            if (TryGetModeForSubSection(subSection, out var subSectionMode))
            {
                PreviewOptions.GameMode = subSectionMode;
            }
            _collapsedGroups.Clear();
            _groupsInitialized = false;
            _sharedColorSettings.Clear();
            SettingsMenu.Instance.Refresh();
        }
    }
}
