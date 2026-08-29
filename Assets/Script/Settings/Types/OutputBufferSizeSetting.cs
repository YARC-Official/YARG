using System;
using YARG.Core.Audio;
using YARG.Localization;

namespace YARG.Settings.Types
{
    /// <summary>
    /// Selects the output buffer size and shows values supported by the active audio driver.
    /// </summary>
    public sealed class OutputBufferSizeSetting : DropdownSetting<int>
    {
        private int  _preferredLength;
        private bool _isDriverControlled;

        public OutputBufferSizeSetting(int value, Action<int> onChange = null) : base(value, onChange, localizable: false)
        {
        }

        public override void UpdateValues()
        {
            _value = 0;
            _possibleValues.Clear();
            _possibleValues.Add(0);
            _preferredLength = 0;
            _isDriverControlled = false;

            if (GlobalAudioHandler.GetOutputBufferInfo() is not { } info)
            {
                return;
            }

            _preferredLength = info.PreferredLength;
            _isDriverControlled = info.IsDriverControlled;
            if (!_isDriverControlled)
            {
                _possibleValues.AddRange(info.SupportedLengths);
            }
        }

        public override string ValueToString(int value)
        {
            if (value == 0)
            {
                if (_isDriverControlled)
                {
                    return Localize.KeyFormat("Settings.Setting.AsioBufferSize.DriverControlledWithSize",
                        _preferredLength);
                }

                return _preferredLength > 0
                    ? Localize.KeyFormat("Settings.Setting.AsioBufferSize.DriverDefaultWithSize", _preferredLength)
                    : Localize.Key("Settings.Setting.AsioBufferSize.DriverDefault");
            }

            return Localize.KeyFormat("Settings.Setting.AsioBufferSize.Samples", value);
        }
    }
}
