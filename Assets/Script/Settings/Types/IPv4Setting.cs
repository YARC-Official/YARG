using System;

namespace YARG.Settings.Types
{
    public class IPv4Setting : AbstractSetting<byte[]>
    {
        public override string AddressableName => "Setting/IPv4";

        public byte Min { get; }
        public byte Max { get; }

        public IPv4Setting(byte[] value, Action<byte[]> onChange = null) : base(onChange)
        {
            //IPv4 range from 0 to 255.
            Min = 0x00;
            Max = 0xFF;

            _value = value;
        }

        public override bool ValueEquals(byte[] value)
        {
            return value == Value;
        }
    }
}

/*
It's easy to sit and scoff at an old man's folly. But also, check out his Adam's apple!

- JackHandey
*/
