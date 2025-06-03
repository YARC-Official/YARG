using System;
using System.Collections.Generic;
using System.Linq;
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
                GameMode.SixFretGuitar => "guitar",

                GameMode.FourLaneDrums => "drums",
                GameMode.FiveLaneDrums => "ghDrums",
                // GameMode.EliteDrums   => "eliteDrums",

                GameMode.ProGuitar => "realGuitar",
                GameMode.ProKeys => "realKeys",

                GameMode.Vocals => "vocals",

                // GameMode.Dj          => "dj",

                _ => null,
            };
        }

        // Returns a list of valid settings for a given game mode
        public static List<string> PossibleProfileSettings(this GameMode gameMode, Dictionary<string, object> dependencyNamesAndValues)
        {
            List<string> unconditionallyValidInAllModes = new List<string>
            {
                "Instrument Select",
                "Engine Preset",
                "Theme Select",
                "Color Profile Select",
                "Camera Preset",
                "Highway Preset",
                "Input Calibration",
                "Note Speed and Highway Length",
            };

            List<string> unconditionalGameModeOptions = gameMode switch
            {
                GameMode.FiveFretGuitar => new List<string>
                {
                    "Lefty Flip",
                    "Range Disable",
                },
                GameMode.FourLaneDrums => new List<string>
                {
                    "Lefty Flip",
                    "Split Tom and Cymbal Lanes in Pro Drums"
                },
                GameMode.FiveLaneDrums => new List<string>
                {
                    "Lefty Flip",
                    "Use Cymbal Models",
                    "Swap Snare and Hi-Hat"
                },
                GameMode.SixFretGuitar => new List<string>
                {
                    "Lefty Flip",
                    "Range Disable",
                },
                _ => new List<string>()
            };

            var possibleOptions = unconditionallyValidInAllModes;
            possibleOptions.AddRange(unconditionalGameModeOptions);

            Dictionary<string, (string dependencyName, Func<object, bool> dependencyCondition)> conditionalGameModeOptions = gameMode switch
            {
                GameMode.FourLaneDrums => new()
                {
                    { "Swap Snare and Hi-Hat", ("Split Tom and Cymbal Lanes in Pro Drums", (object value)=>(bool)value) }
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
                        possibleOptions.Add(dependentSetting);
                    }
                }
            }

            return possibleOptions;
        }
    }
}