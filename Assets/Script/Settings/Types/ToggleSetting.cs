using System;

namespace YARG.Settings.Types
{
    public class ToggleSetting : AbstractSetting<bool>
    {
        public override string AddressableName => "Setting/Toggle";

        public ToggleSetting(bool value, Action<bool> onChange = null) : base(onChange)
        {
            _value = value;
        }

        public override bool ValueEquals(bool value) => value == Value;
    }
}