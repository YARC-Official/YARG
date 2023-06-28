using System;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Data
{
    /// <summary>
    /// A pair of a difficulty character (E, M, H, X, P) and an integer score.
    /// </summary>
    [JsonConverter(typeof(DiffScoreConverter))]
    public struct DiffScore : IComparable<DiffScore>
    {
        public Difficulty difficulty;
        public int score;
        public int stars;

        public int CompareTo(DiffScore other)
        {
            int dc = difficulty.CompareTo(other.difficulty);

            if (dc == 0)
            {
                return score.CompareTo(other.score);
            }

            return dc;
        }

        public static bool operator >(DiffScore operand1, DiffScore operand2)
        {
            return operand1.CompareTo(operand2) > 0;
        }

        public static bool operator <(DiffScore operand1, DiffScore operand2)
        {
            return operand1.CompareTo(operand2) < 0;
        }
    }
}