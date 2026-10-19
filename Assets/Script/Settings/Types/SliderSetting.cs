using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class SliderSetting : AbstractSetting<float>
    {
        public override string AddressableName => "Setting/Slider";

        public float Min { get; private set; }
        public float Max => _maxProvider?.Invoke() ?? _max;
        public bool WholeNumbers { get; }

        private readonly float _max;
        private readonly Func<float> _maxProvider;

        public SliderSetting(float value, float min = float.NegativeInfinity, float max = float.PositiveInfinity,
            Action<float> onChange = null, bool wholeNumbers = false) : base(onChange)
        {
            Min = min;
            _max = max;
            WholeNumbers = wholeNumbers;

            _value = value;
        }

        public SliderSetting(float value, float min, Func<float> maxProvider,
            Action<float> onChange = null, bool wholeNumbers = false) : base(onChange)
        {
            Min = min;
            _max = float.PositiveInfinity;
            _maxProvider = maxProvider;
            WholeNumbers = wholeNumbers;

            _value = value;
        }

        protected override void SetValue(float value)
        {
            if (WholeNumbers)
                value = Mathf.Round(value);

            _value = Mathf.Clamp(value, Min, Max);
        }

        public override bool ValueEquals(float value)
            => Mathf.Approximately(value, Value);
    }
}
