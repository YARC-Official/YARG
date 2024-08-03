using System;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Helpers.Extensions;
using YARG.Menu;
using YARG.Menu.Navigation;
using YARG.Menu.Settings;
using YARG.Settings.Types;

using SystemColor = System.Drawing.Color;
using UnityColor = UnityEngine.Color;

namespace YARG.Settings.Metadata
{
    public partial class PresetSubTab<T>
    {
        private const string CAMERA_PRESET = nameof(CameraPreset);
        private const string COLOR_PROFILE = nameof(ColorProfile);
        private const string ENGINE_PRESET = nameof(EnginePreset);

        private const DurationInputField.Unit ENGINE_UNIT = DurationInputField.Unit.Milliseconds;

        private void BuildForCamera(Transform container, NavigationGroup navGroup,
            CameraPreset cameraPreset)
        {
            SpawnHeader(container, "PresetSettings");
            CreateFields(container, navGroup, CAMERA_PRESET, new List<(string, ISettingType)>()
            {
                (nameof(cameraPreset.FieldOfView), new SliderSetting(cameraPreset.FieldOfView,  40f, 150f)),
                (nameof(cameraPreset.PositionY),   new SliderSetting(cameraPreset.PositionY,    0f, 4f)),
                (nameof(cameraPreset.PositionZ),   new SliderSetting(cameraPreset.PositionZ,    0f, 12f)),
                (nameof(cameraPreset.Rotation),    new SliderSetting(cameraPreset.Rotation,     0f, 180f)),
                (nameof(cameraPreset.FadeLength),  new SliderSetting(cameraPreset.FadeLength,   0f, 5f)),
                (nameof(cameraPreset.CurveFactor), new SliderSetting(cameraPreset.CurveFactor, -3f, 3f)),
            });
        }

        private void UpdateForCamera(CameraPreset cameraPreset)
        {
            float Get(string name) => ((SliderSetting) _settingFields[name]).Value;

            cameraPreset.FieldOfView = Get(nameof(cameraPreset.FieldOfView));
            cameraPreset.PositionY   = Get(nameof(cameraPreset.PositionY));
            cameraPreset.PositionZ   = Get(nameof(cameraPreset.PositionZ));
            cameraPreset.Rotation    = Get(nameof(cameraPreset.Rotation));
            cameraPreset.FadeLength  = Get(nameof(cameraPreset.FadeLength));
            cameraPreset.CurveFactor = Get(nameof(cameraPreset.CurveFactor));
        }

        private void BuildForColor(Transform container, NavigationGroup navGroup,
            ColorProfile colorProfile)
        {
            // Set sub-section
            if (string.IsNullOrEmpty(_subSection))
            {
                _subSection = nameof(GameMode.FiveFretGuitar);
            }

            // Create instrument dropdown
            var dropdown = CreateField(container, COLOR_PROFILE, "Instrument", hasDescription: false,
                new DropdownSetting<string>(_subSection, RefreshForSubSection)
                {
                    nameof(GameMode.FiveFretGuitar),
                    nameof(GameMode.FourLaneDrums),
                    nameof(GameMode.FiveLaneDrums),
                }
            );
            navGroup.AddNavigatable(dropdown.gameObject);

            // Set the preview type
            if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
            {
                // Yucky.
                // TODO: Redo this whole system!
                trackPreviewBuilder.StartingGameMode = (GameMode) Enum.Parse(typeof(GameMode), _subSection);
            }
            else
            {
                YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
            }

            // Header
            SpawnHeader(container, "PresetSettings");

            // Reflection is slow, however, it's more maintainable in this case
            var gameModeProfile = GetSelectedGameModeProfile(colorProfile);
            foreach (var field in gameModeProfile.GetType().GetFields())
            {
                // Skip non-color fields
                if (field.FieldType != typeof(SystemColor)) continue;

                // Get the starting value
                var color = ((SystemColor) field.GetValue(gameModeProfile)).ToUnityColor();

                // Add field
                var visual = CreateField(container, COLOR_PROFILE, field.Name, hasDescription: false,
                    new ColorSetting(color, true));
                navGroup.AddNavigatable(visual.gameObject);
            }
        }

        private void UpdateForColor(ColorProfile colorProfile)
        {
            // Reflection is slow, however, it's more maintainable in this case
            var instrumentProfile = GetSelectedGameModeProfile(colorProfile);
            foreach (var field in instrumentProfile.GetType().GetFields())
            {
                // Skip non-color fields
                if (field.FieldType != typeof(SystemColor)) continue;

                // Get the setting
                var setting = _settingFields[field.Name];

                // Set value
                var color = ((UnityColor) setting.ValueAsObject).ToSystemColor();
                field.SetValue(instrumentProfile, color);
            }
        }

        private object GetSelectedGameModeProfile(ColorProfile c)
        {
            return _subSection switch
            {
                nameof(GameMode.FiveFretGuitar) => c.FiveFretGuitar,
                nameof(GameMode.FourLaneDrums)  => c.FourLaneDrums,
                nameof(GameMode.FiveLaneDrums)  => c.FiveLaneDrums,
                _ => throw new Exception("Unreachable.")
            };
        }

        private void BuildForEngine(Transform container, NavigationGroup navGroup,
            EnginePreset enginePreset)
        {
            // Set sub-section
            if (string.IsNullOrEmpty(_subSection))
            {
                _subSection = nameof(EnginePreset.FiveFretGuitarPreset);
            }

            // Create game mode dropdown
            var dropdown = CreateField(container, ENGINE_PRESET, "GameMode", hasDescription: false,
                new DropdownSetting<string>(_subSection, RefreshForSubSection)
                {
                    nameof(EnginePreset.FiveFretGuitarPreset),
                    nameof(EnginePreset.DrumsPreset),
                    nameof(EnginePreset.VocalsPreset)
                }
            );
            navGroup.AddNavigatable(dropdown.gameObject);

            // Set the preview type
            if (PreviewBuilder is TrackPreviewBuilder trackPreviewBuilder)
            {
                trackPreviewBuilder.StartingGameMode = _subSection switch
                {
                    nameof(EnginePreset.FiveFretGuitarPreset) => GameMode.FiveFretGuitar,
                    nameof(EnginePreset.DrumsPreset)          => GameMode.FourLaneDrums,
                    // nameof(EnginePreset.VocalsPreset)         => GameMode.Vocals, // Uncomment once we have vocals visual preview
                    nameof(EnginePreset.VocalsPreset)         => trackPreviewBuilder.StartingGameMode, // Do not change
                    _ => throw new Exception("Unreachable.")
                };
            }
            else
            {
                YargLogger.LogWarning("This sub-tab's preview builder should be a track preview!");
            }

            // Header
            SpawnHeader(container, "PresetSettings");

            // Spawn in the correct settings
            switch (_subSection)
            {
                case nameof(EnginePreset.FiveFretGuitarPreset):
                {
                    var preset = enginePreset.FiveFretGuitar;

                    CreateFields(container, navGroup, ENGINE_PRESET, new List<(string, ISettingType)>()
                    {
                        (
                            nameof(preset.AntiGhosting),
                            new ToggleSetting(preset.AntiGhosting)
                        ),
                        (
                            nameof(preset.InfiniteFrontEnd),
                            new ToggleSetting(preset.InfiniteFrontEnd)
                        ),
                        (
                            nameof(preset.HopoLeniency),
                            new DurationSetting(preset.HopoLeniency, ENGINE_UNIT)
                        ),
                        (
                            nameof(preset.StrumLeniency),
                            new DurationSetting(preset.StrumLeniency, ENGINE_UNIT)
                        ),
                        (
                            nameof(preset.StrumLeniencySmall),
                            new DurationSetting(preset.StrumLeniencySmall, ENGINE_UNIT)
                        ),
                        (
                            nameof(preset.HitWindow.IsDynamic),
                            // The settings menu has to be refreshed so the hit window setting below updates
                            new ToggleSetting(preset.HitWindow.IsDynamic, (value) =>
                            {
                                // If this gets called, it refreshes before it can update.
                                // We must update the dynamic hit window bool here.
                                preset.HitWindow.IsDynamic = value;

                                SettingsMenu.Instance.RefreshAndKeepPosition();
                            })
                        ),
                        (
                            nameof(preset.HitWindow),
                            new HitWindowSetting(preset.HitWindow)
                        ),
                        (
                            nameof(preset.HitWindow.FrontToBackRatio),
                            new SliderSetting((float) preset.HitWindow.FrontToBackRatio, 0f, 2f)
                        )
                    });

                    break;
                }
                case nameof(EnginePreset.DrumsPreset):
                {
                    var preset = enginePreset.Drums;

                    CreateFields(container, navGroup, ENGINE_PRESET, new List<(string, ISettingType)>()
                    {
                        (
                            nameof(preset.HitWindow.IsDynamic),
                            // The settings menu has to be refreshed so the hit window setting below updates
                            new ToggleSetting(preset.HitWindow.IsDynamic, (value) =>
                            {
                                // If this gets called, it refreshes before it can update.
                                // We must update the dynamic hit window bool here.
                                preset.HitWindow.IsDynamic = value;

                                SettingsMenu.Instance.RefreshAndKeepPosition();
                            })
                        ),
                        (
                            nameof(preset.HitWindow),
                            new HitWindowSetting(preset.HitWindow)
                        ),
                        (
                            nameof(preset.HitWindow.FrontToBackRatio),
                            new SliderSetting((float) preset.HitWindow.FrontToBackRatio, 0f, 2f)
                        )
                    });

                    break;
                }
                case nameof(EnginePreset.VocalsPreset):
                {
                    var preset = enginePreset.Vocals;

                    CreateFields(container, navGroup, ENGINE_PRESET, new List<(string, ISettingType)>()
                    {
                        (
                            nameof(preset.WindowSizeE),
                            new SliderSetting((float) preset.WindowSizeE, 0f, 3f)
                        ),
                        (
                            nameof(preset.WindowSizeM),
                            new SliderSetting((float) preset.WindowSizeM, 0f, 3f)
                        ),
                        (
                            nameof(preset.WindowSizeH),
                            new SliderSetting((float) preset.WindowSizeH, 0f, 3f)
                        ),
                        (
                            nameof(preset.WindowSizeX),
                            new SliderSetting((float) preset.WindowSizeX, 0f, 3f)
                        ),
                        (
                            nameof(preset.HitPercentE),
                            new SliderSetting((float) preset.HitPercentE, 0f, 1f)
                        ),
                        (
                            nameof(preset.HitPercentM),
                            new SliderSetting((float) preset.HitPercentM, 0f, 1f)
                        ),
                        (
                            nameof(preset.HitPercentH),
                            new SliderSetting((float) preset.HitPercentH, 0f, 1f)
                        ),
                        (
                            nameof(preset.HitPercentX),
                            new SliderSetting((float) preset.HitPercentX, 0f, 1f)
                        )
                    });

                    break;
                }
                default:
                    throw new Exception("Unreachable.");
            }
        }

        private void UpdateForEngine(EnginePreset enginePreset)
        {
            double GetDouble(string name) => ((AbstractSetting<double>) _settingFields[name]).Value;
            bool   GetBool(string name)   => ((AbstractSetting<bool>)   _settingFields[name]).Value;

            switch (_subSection)
            {
                case nameof(EnginePreset.FiveFretGuitarPreset):
                {
                    var preset = enginePreset.FiveFretGuitar;

                    preset.AntiGhosting       = GetBool(nameof(preset.AntiGhosting));
                    preset.InfiniteFrontEnd   = GetBool(nameof(preset.InfiniteFrontEnd));
                    preset.HopoLeniency       = GetDouble(nameof(preset.HopoLeniency));
                    preset.StrumLeniency      = GetDouble(nameof(preset.StrumLeniency));
                    preset.StrumLeniencySmall = GetDouble(nameof(preset.StrumLeniencySmall));

                    // Get the value of the hit window first, so the other stuff can be overridden
                    preset.HitWindow = ((HitWindowSetting) _settingFields[nameof(preset.HitWindow)]).Value;

                    // Override the other settings after
                    preset.HitWindow.IsDynamic =
                        GetBool(nameof(preset.HitWindow.IsDynamic));
                    preset.HitWindow.FrontToBackRatio =
                        ((SliderSetting) _settingFields[nameof(preset.HitWindow.FrontToBackRatio)]).Value;

                    break;
                }
                case nameof(EnginePreset.DrumsPreset):
                {
                    var preset = enginePreset.Drums;

                    // Get the value of the hit window first, so the other stuff can be overridden
                    preset.HitWindow = ((HitWindowSetting) _settingFields[nameof(preset.HitWindow)]).Value;

                    // Override the other settings after
                    preset.HitWindow.IsDynamic =
                        GetBool(nameof(preset.HitWindow.IsDynamic));
                    preset.HitWindow.FrontToBackRatio =
                        ((SliderSetting) _settingFields[nameof(preset.HitWindow.FrontToBackRatio)]).Value;

                    break;
                }
                case nameof(EnginePreset.VocalsPreset):
                {
                    var preset = enginePreset.Vocals;

                    preset.WindowSizeE = ((SliderSetting) _settingFields[nameof(preset.WindowSizeE)]).Value;
                    preset.WindowSizeM = ((SliderSetting) _settingFields[nameof(preset.WindowSizeM)]).Value;
                    preset.WindowSizeH = ((SliderSetting) _settingFields[nameof(preset.WindowSizeH)]).Value;
                    preset.WindowSizeX = ((SliderSetting) _settingFields[nameof(preset.WindowSizeX)]).Value;
                    preset.HitPercentE = ((SliderSetting) _settingFields[nameof(preset.HitPercentE)]).Value;
                    preset.HitPercentM = ((SliderSetting) _settingFields[nameof(preset.HitPercentM)]).Value;
                    preset.HitPercentH = ((SliderSetting) _settingFields[nameof(preset.HitPercentH)]).Value;
                    preset.HitPercentX = ((SliderSetting) _settingFields[nameof(preset.HitPercentX)]).Value;

                    break;
                }
                default:
                    throw new Exception("Unreachable.");
            }
        }

        private void RefreshForSubSection(string subSection)
        {
            _subSection = subSection;
            SettingsMenu.Instance.Refresh();
        }
    }
}