using System;
using UnityEngine;

namespace YARG.Settings.Types
{
    public class DMXChannelsSetting : AbstractSetting<int[]>
    {
        public override string AddressableName => "Setting/DMXChannels";

        public int Min { get; }
        public int Max { get; }

        public DMXChannelsSetting(int[] value, Action<int[]> onChange = null) : base(onChange)
        {
            //DMX channels are 8-bit, so the range is 0-255
            Min = 0;
            Max = 255;

            _value = value;
        }
        public override bool ValueEquals(int[] value)
        {
            return value == Value;
        }
    }
}