using System;

namespace YARG.Settings.Types
{
    public class DMXChannelsSetting : AbstractSetting<int[]>
    {
        public override string AddressableName => "Setting/DMXChannels";

        public int Min { get; }
        public int Max { get; }

        public DMXChannelsSetting(int[] value, Action<int[]> onChange = null) : base(onChange)
        {
            // DMX channels range from 1 to 512. (0 is the start code channel)
            Min = 1;
            Max = 512;

            _value = value;
        }

        public override bool ValueEquals(int[] value)
        {
            return value == Value;
        }
    }
}