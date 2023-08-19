using System;

namespace YARG.Settings.Types
{
    public class VolumeSetting : SliderSetting
    {
        public override string AddressableName => "Setting/Volume";

        public VolumeSetting(float value, Action<float> onChange = null) : base(value, 0f, 1f, onChange)
        {
        }
    }
}