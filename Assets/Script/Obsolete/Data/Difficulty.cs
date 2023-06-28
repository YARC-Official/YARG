using System;

namespace YARG.Data
{
    public enum Difficulty
    {
        EASY = 0,
        MEDIUM,
        HARD,
        EXPERT,
        EXPERT_PLUS // Drums double bass only
    }

    public static class DifficultyExtensions
    {
        public static char ToChar(this Difficulty diff)
        {
            return diff switch
            {
                Difficulty.EASY        => 'E',
                Difficulty.MEDIUM      => 'M',
                Difficulty.HARD        => 'H',
                Difficulty.EXPERT      => 'X',
                Difficulty.EXPERT_PLUS => 'P',
                _                      => '?'
            };
        }

        public static Difficulty FromChar(char diff)
        {
            return diff switch
            {
                'E' => Difficulty.EASY,
                'M' => Difficulty.MEDIUM,
                'H' => Difficulty.HARD,
                'X' => Difficulty.EXPERT,
                'P' => Difficulty.EXPERT_PLUS,
                _   => throw new System.Exception("Unknown difficulty.")
            };
        }

        public static string ToStringName(this Difficulty difficulty)
        {
            return difficulty switch
            {
                Difficulty.EASY        => "Easy",
                Difficulty.MEDIUM      => "Medium",
                Difficulty.HARD        => "Hard",
                Difficulty.EXPERT      => "Expert",
                Difficulty.EXPERT_PLUS => "Expert+",
                _                      => "Unknown"
            };
        }
    }
}