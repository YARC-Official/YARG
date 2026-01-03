using System;
using YARG.Core.Audio;

namespace YARG.Settings.Types
{
    public class OutputDeviceSetting : DropdownSetting<string>
    {
        public OutputDeviceSetting(string value, Action<string> onChange = null) : base(value, onChange, localizable: false)
        {
        }

        public override void UpdateValues()
        {
            _possibleValues.Clear();

            foreach ((int, string name) device in GlobalAudioHandler.GetAllOutputDevices())
            {
                _possibleValues.Add(device.name);
            }
        }
    }
}
