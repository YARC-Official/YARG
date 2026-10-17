using System;

namespace YARG.Settings.Types
{
    // A user-browsable folder path. An empty value means "not set" -- callers
    // should fall back to their own auto-detection when Value is null/empty.
    public class FolderPathSetting : AbstractSetting<string>
    {
        public override string AddressableName => "Setting/FolderPath";

        public FolderPathSetting(string value, Action<string> onChange = null) : base(onChange)
        {
            _value = value ?? string.Empty;
        }

        protected override void SetValue(string value)
        {
            _value = value ?? string.Empty;
        }

        public override bool ValueEquals(string value) => value == Value;
    }
}
