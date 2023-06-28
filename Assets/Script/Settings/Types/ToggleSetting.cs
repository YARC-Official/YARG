using System;

namespace YARG.Settings.Types
{
    public class ToggleSetting : AbstractSetting<bool>
    {
        private bool _data;

        public override bool Data
        {
            get => _data;
            set
            {
                _data = value;
                base.Data = value;
            }
        }

        public override string AddressableName => "Setting/Toggle";

        public ToggleSetting(bool value, Action<bool> onChange = null) : base(onChange)
        {
            _data = value;
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