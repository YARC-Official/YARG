using System;

namespace YARG.Settings.Types
{
    public class VolumeSetting : SliderSetting
    {
        public override string AddressableName => "Setting/Volume";

        public VolumeSetting(float value, Action<float> onChange = null)
            : this(value, 1f, onChange)
        {
        }

        public VolumeSetting(float value, float maximum, Action<float> onChange = null)
            : base(value, 0f, maximum, onChange)
        {
        }
    }
}
