using System;

namespace YARG.Core.Game.Settings
{
    public class SettingRangeAttribute : Attribute
    {
        public float Min { get; }
        public float Max { get; }

        public SettingRangeAttribute(float min = float.NegativeInfinity, float max = float.PositiveInfinity)
        {
            Min = min;
            Max = max;
        }
    }
}