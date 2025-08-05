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
                // GameMode.EliteDrums  => "eliteDrums",

                GameMode.ProGuitar      => "realGuitar",
                GameMode.ProKeys        => "realKeys",

                GameMode.Vocals         => "vocals",

                // GameMode.Dj          => "dj",

                _                       => null,
            };
        }

        // Returns a list of valid settings for a given game mode
        public static List<string> PossibleProfileSettings(this GameMode gameMode, Dictionary<string, object> dependencyNamesAndValues)
        {
            List<string> unconditionallyValidInAllModes = new List<string>
            {
                ProfileSettingStrings.INSTRUMENT_SELECT,
                ProfileSettingStrings.ENGINE_PRESET,
                ProfileSettingStrings.THEME_SELECT,
                ProfileSettingStrings.COLOR_PROFILE_SELECT,
                ProfileSettingStrings.CAMERA_PRESET,
                ProfileSettingStrings.HIGHWAY_PRESET,
                ProfileSettingStrings.INPUT_CALIBRATION,
                ProfileSettingStrings.NOTE_SPEED_AND_HIGHWAY_LENGTH,
            };

            List<string> unconditionalGameModeOptions = gameMode switch
            {
                GameMode.FiveFretGuitar => new List<string>
                {
                    ProfileSettingStrings.LEFTY_FLIP,
                    ProfileSettingStrings.RANGE_DISABLE,
                },
                GameMode.FourLaneDrums => new List<string>
                {
                    ProfileSettingStrings.LEFTY_FLIP,
                    ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS,
                },
                GameMode.FiveLaneDrums => new List<string>
                {
                    ProfileSettingStrings.LEFTY_FLIP,
                    ProfileSettingStrings.USE_CYMBAL_MODELS,
                    ProfileSettingStrings.SWAP_SNARE_AND_HI_HAT,
                },
                GameMode.SixFretGuitar => new List<string>
                {
                    ProfileSettingStrings.LEFTY_FLIP,
                    ProfileSettingStrings.RANGE_DISABLE,
                },
                _ => new List<string>()
            };

            var possibleOptions = unconditionallyValidInAllModes;
            possibleOptions.AddRange(unconditionalGameModeOptions);

            // Split into a private method for readability
            possibleOptions.AddRange(ConditionalGameModeSettings(gameMode, dependencyNamesAndValues));

            return possibleOptions;
        }

        private static List<string> ConditionalGameModeSettings(GameMode gameMode, Dictionary<string, object> dependencyNamesAndValues)
        {
            var conditionalSettings = new List<string>();

            Dictionary<string, (string dependencyName, Func<object, bool> dependencyCondition)> conditionalGameModeOptions = gameMode switch
            {
                GameMode.FourLaneDrums => new()
                {
                    { ProfileSettingStrings.SWAP_SNARE_AND_HI_HAT, (ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS, (object value)=>(bool)value) },
                    { ProfileSettingStrings.SWAP_CRASH_AND_RIDE, (ProfileSettingStrings.SPLIT_TOM_AND_CYMBAL_LANES_IN_PRO_DRUMS, (object value)=>(bool)value) }
                },
                _ => new()
            };

            // Some settings should only show if a different setting's value meets some condition. For each one of those...
            foreach ((var dependentSetting, var dependencyNameAndCondition) in conditionalGameModeOptions)
            {
                // ...if we received information about the dependency...
                if (dependencyNamesAndValues.ContainsKey(dependencyNameAndCondition.dependencyName))
                {
                    (var dependencyName, var dependencyCondition) = (dependencyNameAndCondition.dependencyName, dependencyNameAndCondition.dependencyCondition);
                    var dependencyValue = dependencyNamesAndValues[dependencyName];

                    // ...and the dependency's value satisfies the condition...
                    if (dependencyCondition(dependencyValue))
                    {
                        // ...then add the dependent setting as a possible option!
                        conditionalSettings.Add(dependentSetting);
                    }
                }
            }

            return conditionalSettings;
        }
    }
}