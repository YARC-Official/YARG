using System;
using YARG.Core;

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
    }
}