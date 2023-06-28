using System;
using Newtonsoft.Json;
using YARG.Serialization;

namespace YARG.Data
{
    /// <summary>
    /// A pair of a difficulty character (E, M, H, X, P) and a percent.
    /// </summary>
    [JsonConverter(typeof(DiffPercentConverter))]
    public struct DiffPercent : IComparable<DiffPercent>
    {
        public Difficulty difficulty;
        public float percent;

        public int CompareTo(DiffPercent other)
        {
            int dc = difficulty.CompareTo(other.difficulty);

            if (dc == 0)
            {
                return percent.CompareTo(other.percent);
            }

            return dc;
        }

        public static bool operator >(DiffPercent operand1, DiffPercent operand2)
        {
            return operand1.CompareTo(operand2) > 0;
        }

        public static bool operator <(DiffPercent operand1, DiffPercent operand2)
        {
            return operand1.CompareTo(operand2) < 0;
        }
    }
}