using System;
using YARG.Core.Audio;

namespace YARG.Settings.Types
{
    public class OutputDeviceSetting : DropdownSetting<string>
    {
        public OutputDeviceSetting(string value, Action<string> onChange = null) : base(value, onChange, localizable: false)
        {
        }

        public override void UpdateValues() => UpdateValues(GlobalAudioHandler.GetOutputMode(Value));

        public void UpdateValues(AudioOutputMode mode)
        {
            _possibleValues.Clear();

            foreach (var device in GlobalAudioHandler.GetAllOutputDevices())
            {
                if (GlobalAudioHandler.GetOutputMode(device.name) == mode)
                {
                    _possibleValues.Add(device.name);
                }
            }
        }

        public string FindAvailable(string preferred)
        {
            if (_possibleValues.Contains(preferred))
            {
                return preferred;
            }

            return _possibleValues.Count > 0 ? _possibleValues[0] : null;
        }

        public override string ValueToString(string value)
        {
            if (value.StartsWith(ASIO_PREFIX, StringComparison.Ordinal))
            {
                return value.Substring(ASIO_PREFIX.Length);
            }

            if (value.StartsWith(WASAPI_PREFIX, StringComparison.Ordinal))
            {
                return value.Substring(WASAPI_PREFIX.Length);
            }

            return value;
        }

        private const string ASIO_PREFIX = "ASIO: ";
        private const string WASAPI_PREFIX = "WASAPI: ";
    }
}
