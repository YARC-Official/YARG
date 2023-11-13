using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class SliderSetting : AbstractSetting<float>
    {
        public override string AddressableName => "Setting/Slider";

        public float Min { get; private set; }
        public float Max { get; private set; }

        public SliderSetting(float value, float min = float.NegativeInfinity, float max = float.PositiveInfinity,
            Action<float> onChange = null) : base(onChange)
        {
            Min = min;
            Max = max;

            DataField = value;
        }

        protected override void SetDataField(float value)
        {
            DataField = Mathf.Clamp(value, Min, Max);
        }

        public override bool IsSettingDataEqual(object obj)
        {
            if (obj is not float other)
            {
                return false;
            }

            return Mathf.Approximately(other, Data);
        }
    }
}