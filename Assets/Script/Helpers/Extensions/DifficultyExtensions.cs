using System;
using YARG.Core;
using YARG.Settings;

namespace YARG.Helpers.Extensions
{
    public static class DifficultyExtensions
    {
        public static char ToChar(this Difficulty diff)
        {
            return diff switch
            {
                Difficulty.Easy       => 'E',
                Difficulty.Medium     => 'M',
                Difficulty.Hard       => 'H',
                Difficulty.Expert     => 'X',
                Difficulty.ExpertPlus => 'P',

                _ => '?'
            };
        }

        public static Difficulty FromChar(char diff)
        {
            return diff switch
            {
                'E' => Difficulty.Easy,
                'M' => Difficulty.Medium,
                'H' => Difficulty.Hard,
                'X' => Difficulty.Expert,
                'P' => Difficulty.ExpertPlus,

                _ => throw new Exception("Unknown difficulty.")
            };
        }

        public static string ToDisplayName(this Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.Easy       => "Easy",
                Difficulty.Medium     => "Medium",
                Difficulty.Hard       => "Hard",
                Difficulty.Expert     => "Expert",
                Difficulty.ExpertPlus => "Expert+",

                _ => "Unknown"
            };
        }

        /// <summary>
        /// Returns the scale by which the note speed should be adjusted
        /// according to the difficulty of the track.
        /// </summary>
        public static float NoteSpeedScale(this Difficulty difficulty)
        {
            if (!SettingsManager.Settings.ReduceNoteSpeedByDifficulty.Value)
            {
                return 1f;
            }
            
            return difficulty switch
            {
                Difficulty.Easy       => 0.421875f,
                Difficulty.Medium     => 0.5625f,
                Difficulty.Hard       => 0.75f,
                Difficulty.Expert     => 1f,
                Difficulty.ExpertPlus => 1f,

                _ => 1f
            };
        }
    }
}