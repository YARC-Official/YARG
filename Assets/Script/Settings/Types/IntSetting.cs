using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class IntSetting : AbstractSetting<int>
    {
        public override string AddressableName => "Setting/Int";

        public int Min { get; }
        public int Max { get; }

        public IntSetting(int value, int min = int.MinValue, int max = int.MaxValue, Action<int> onChange = null) :
            base(onChange)
        {
            Min = min;
            Max = max;

            _value = value;
        }

        protected override void SetValue(int value)
        {
            _value = Mathf.Clamp(value, Min, Max);
        }

        public override bool ValueEquals(object obj)
        {
            if (obj is not int other)
            {
                return false;
            }

            return other == Value;
        }
    }
}