using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class IntSetting : AbstractSetting<int>
    {
        public override string AddressableName => "Setting/Int";

        public int Min { get; private set; }
        public int Max { get; private set; }

        public IntSetting(int value, int min = int.MinValue, int max = int.MaxValue, Action<int> onChange = null) :
            base(onChange)
        {
            Min = min;
            Max = max;

            DataField = value;
        }

        protected override void SetDataField(int value)
        {
            DataField = Mathf.Clamp(value, Min, Max);
        }

        public override bool IsSettingDataEqual(object obj)
        {
            if (obj is not int other)
            {
                return false;
            }

            return other == Data;
        }
    }
}