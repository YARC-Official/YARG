using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class SliderSetting : AbstractSetting<float>
    {
        public override string AddressableName => "Setting/Slider";

        public float Min { get; private set; }
        public float Max { get; private set; }
        public bool WholeNumbers { get; }
        public bool ClampMax { get; }
        public Func<float> InputMax { get; }

        public SliderSetting(float value, float min = float.NegativeInfinity, float max = float.PositiveInfinity,
            Action<float> onChange = null, bool wholeNumbers = false, bool clampMax = true,
            Func<float> inputMax = null) : base(onChange)
        {
            Min = min;
            Max = max;
            WholeNumbers = wholeNumbers;
            ClampMax = clampMax;
            InputMax = inputMax;

            _value = value;
        }

        protected override void SetValue(float value)
        {
            if (WholeNumbers)
                value = Mathf.Round(value);

            _value = ClampMax ? Mathf.Clamp(value, Min, Max) : Mathf.Max(value, Min);
        }

        public override bool ValueEquals(float value)
            => Mathf.Approximately(value, Value);
    }
}
