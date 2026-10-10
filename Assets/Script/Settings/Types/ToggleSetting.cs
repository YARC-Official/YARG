// This abstraction allows non-boolean settings to render as toggle controls in the UI.

using System;

namespace YARG.Settings.Types
{
    public interface IToggleSetting : ISettingType
    {
        bool Value { get; set; }
    }

    public class ToggleSetting : AbstractSetting<bool>, IToggleSetting
    {
        public override string AddressableName => "Setting/Toggle";

        public ToggleSetting(bool value, Action<bool> onChange = null) : base(onChange)
        {
            _value = value;
        }

        public override bool ValueEquals(bool value) => value == Value;
    }
}
