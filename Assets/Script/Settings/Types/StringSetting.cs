using System;
using YARG.Core.Game;

namespace YARG.Settings.Types
{
    public class StringSetting : AbstractSetting<string>
    {
        public override  string     AddressableName => "Setting/String";

        public StringSetting(string value, Action<string> onChange = null) : base(onChange)
        {
            _value = value;
        }

        public override bool ValueEquals(string other) => Value == other;
    }
}