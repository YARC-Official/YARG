using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class ColorSetting : AbstractSetting<Color>
    {
        public override string AddressableName => "Setting/Color";

        public bool AllowTransparency { get; }

        public ColorSetting(Color value, bool allowTransparency, Action<Color> onChange = null) : base(onChange)
        {
            AllowTransparency = allowTransparency;

            _value = value;
        }

        protected override void SetValue(Color value)
        {
            if (!AllowTransparency)
            {
                value.a = 1f;
            }

            _value = value;
        }

        public override bool ValueEquals(object obj)
        {
            if (obj is not Color color)
            {
                return false;
            }

            return color == Value;
        }
    }
}