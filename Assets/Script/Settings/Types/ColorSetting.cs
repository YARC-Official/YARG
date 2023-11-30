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

            DataField = value;
        }

        protected override void SetDataField(Color value)
        {
            if (!AllowTransparency)
            {
                value.a = 1f;
            }

            DataField = value;
        }

        public override bool IsSettingDataEqual(object obj)
        {
            if (obj is not Color color)
            {
                return false;
            }

            return color == Data;
        }
    }
}