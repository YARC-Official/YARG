using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Assets.Script.Helpers;
using YARG.Core;

namespace YARG.Helpers.Extensions
{
    public static class GameModeExtensions
    {
        public static string ToResourceName(this GameMode instrument)
        {
            return instrument switch
            {
                GameMode.FiveFretGuitar => "guitar",
                GameMode.SixFretGuitar  => "guitar",

                GameMode.FourLaneDrums  => "drums",
                GameMode.FiveLaneDrums  => "ghDrums",
                GameMode.EliteDrums  => "eliteDrums",

                GameMode.ProGuitar      => "realGuitar",
                GameMode.ProKeys        => "realKeys",

                GameMode.Vocals         => "vocals",

                // GameMode.Dj          => "dj",

                _                       => null,
            };
        }

        // Returns a list of valid settings for a given game mode. Each setting can optionally
        // come with override text, for settings that should have different names between profile
        // types
        public static List<(string setting, string? overrideText)> PossibleProfileSettings(
            this GameMode gameMode, Dictionary<string, object> dependencyNamesAndValues)
        {
            List<(string setting, string? overrideText)> unconditionallyValidInAllModes = new()
            {
                (ProfileSettingStrings.INSTRUMENT_SELECT, null),
                (ProfileSettingStrings.ENGINE_PRESET, null),
                (ProfileSettingStrings.ROCK_METER_PRESET, null),
                (ProfileSettingStrings.INPUT_CALIBRATION, null),
            };

            List<(string setting, string? overrideText)> unconditionallyValidInAllModesExceptVocals = new()
            {
                (ProfileSettingStrings.THEME_SELECT, null),
                (ProfileSettingStrings.COLOR_PROFILE_SELECT, null),
                (ProfileSettingStrings.CAMERA_PRESET, null),
                (ProfileSettingStrings.HIGHWAY_PRESET, null),
                (ProfileSettingStrings.INPUT_CALIBRATION, null),
                (ProfileSettingStrings.NOTE_SPEED_AND_HIGHWAY_LENGTH, null),
            };

            List<(string setting, string? overrideText)> unconditionalOptionsPerGameMode = gameMode switch
            {
                GameMode.FiveFretGuitar => new()
                {
                    (ProfileSettingStrings.LEFTY_FLIP, null),
                    (ProfileSettingStrings.RANGE_DISABLE, "RANGE SHIFT MARKERS"),
                },
                GameMode.EliteDrums => new()
                {
                    (ProfileSettingStrings.LEFTY_FLIP, null),
                    (ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS, null),
                    (ProfileSettingStrings.DRUM_STAR_POWER_ACTIVATION_TYPE, null),
                    (ProfileSettingStrings.USE_CYMBAL_MODELS, "USE CYMBAL MODELS IN 5-LANE"),
                    (ProfileSettingStrings.SWAP_SNARE_AND_HI_HAT,
                        (bool)dependencyNamesAndValues[ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS] ?
                        "SWAP SNARE AND HI-HAT LANES" : "SWAP SNARE AND HI-HAT LANES IN 5-LANE"
                    ),
                },
                GameMode.FourLaneDrums => new()
                {
                    (ProfileSettingStrings.LEFTY_FLIP, null),
                    (ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS, null),
                    (ProfileSettingStrings.DRUM_STAR_POWER_ACTIVATION_TYPE, null),
                },
                GameMode.FiveLaneDrums => new()
                {
                    (ProfileSettingStrings.LEFTY_FLIP, null),
                    (ProfileSettingStrings.USE_CYMBAL_MODELS, "USE CYMBAL MODELS"),
                    (ProfileSettingStrings.SWAP_SNARE_AND_HI_HAT, "SWAP SNARE AND HI-HAT LANES"),
                    (ProfileSettingStrings.DRUM_STAR_POWER_ACTIVATION_TYPE, null),
                },
                GameMode.SixFretGuitar => new()
                {
                    (ProfileSettingStrings.LEFTY_FLIP, null),
                    (ProfileSettingStrings.RANGE_DISABLE, "5-LANE RANGE SHIFT MARKERS"),
                },
                GameMode.ProKeys => new()
                {
                    (ProfileSettingStrings.RANGE_DISABLE, "5-LANE RANGE SHIFT MARKERS")
                },
                _ => new()
            };

            var possibleOptions = unconditionallyValidInAllModes;

            if (gameMode is not GameMode.Vocals)
            {
                possibleOptions.AddRange(unconditionallyValidInAllModesExceptVocals);
            }

            possibleOptions.AddRange(unconditionalOptionsPerGameMode);

            // Split into a private method for readability
            possibleOptions.AddRange(ConditionalGameModeSettings(gameMode, dependencyNamesAndValues));

            return possibleOptions;
        }

        private static List<(string setting, string? overrideText)> ConditionalGameModeSettings(GameMode gameMode, Dictionary<string, object> dependencyNamesAndValues)
        {
            var conditionalSettings = new List<(string setting, string? overrideText)>();

            Dictionary<string, (string dependencyName, Func<object, bool> dependencyCondition, string overrideText)> conditionalGameModeOptions = gameMode switch
            {
                GameMode.EliteDrums => new()
                {
                    {
                        ProfileSettingStrings.SWAP_CRASH_AND_RIDE,
                        (
                            ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS,
                            (object value)=>(bool)value,
                            "SWAP CRASH AND RIDE LANES IN PRO DRUMS"
                        )
                    }
                },
                GameMode.FourLaneDrums => new()
                {
                    {
                        ProfileSettingStrings.SWAP_SNARE_AND_HI_HAT,
                        (
                            ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS,
                            (object value)=>(bool)value,
                            "SWAP SNARE AND HI-HAT LANES"
                        )
                    },
                    {
                        ProfileSettingStrings.SWAP_CRASH_AND_RIDE,
                        (
                            ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS,
                            (object value)=>(bool)value,
                            "SWAP CRASH AND RIDE LANES IN PRO DRUMS"
                        )
                    }
                },
                _ => new()
            };

            // Some settings should only show if a different setting's value meets some condition. For each one of those...
            foreach ((var dependentSetting, var dependencyData) in conditionalGameModeOptions)
            {
                // ...if we received information about the dependency...
                if (dependencyNamesAndValues.ContainsKey(dependencyData.dependencyName))
                {
                    (var dependencyName, var dependencyCondition) = (dependencyData.dependencyName, dependencyData.dependencyCondition);
                    var dependencyValue = dependencyNamesAndValues[dependencyName];

                    // ...and the dependency's value satisfies the condition...
                    if (dependencyCondition(dependencyValue))
                    {
                        // ...then add the dependent setting as a possible option!
                        conditionalSettings.Add((dependentSetting, dependencyData.overrideText));
                    }
                }
            }

            return conditionalSettings;
        }
    }
}