using System.Collections.Generic;
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
                // GameMode.EliteDrums   => "eliteDrums",

                GameMode.ProGuitar      => "realGuitar",
                GameMode.ProKeys        => "realKeys",

                GameMode.Vocals         => "vocals",

                // GameMode.Dj          => "dj",

                _                       => null,
            };
        }

        // Returns a list of valid settings for a given game mode
        public static List<string> PossibleProfileSettings(this GameMode gameMode)
        {
            List<string> validInAllModes = new List<string>
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

            List<string> gameModeOptions = gameMode switch
            {
                GameMode.FiveFretGuitar => new List<string>
                {
                    "Lefty Flip",
                    "Range Disable",
                },
                GameMode.FourLaneDrums => new List<string>
                {
                    "Lefty Flip",
                },
                GameMode.FiveLaneDrums => new List<string>
                {
                    "Lefty Flip",
                },
                GameMode.SixFretGuitar => new List<string>
                {
                    "Lefty Flip",
                    "Range Disable",
                },
                _ => new List<string>()
            };

            gameModeOptions.AddRange(validInAllModes);
            return gameModeOptions;
        }
    }
}