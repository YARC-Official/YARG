using System;

namespace YARG.Settings.Types
{
    public class ToggleSetting : AbstractSetting<bool>
    {
        public override string AddressableName => "Setting/Toggle";

        public ToggleSetting(bool value, Action<bool> onChange = null) : base(onChange)
        {
            DataField = value;
        }

        public override bool IsSettingDataEqual(object obj)
        {
            if (obj is not bool other)
            {
                return false;
            }

            return other == Data;
        }
    }
}