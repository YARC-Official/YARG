using System;
using YARG.Core.Audio;
using YARG.Localization;

namespace YARG.Settings.Types
{
    public class OutputChannelSetting : DropdownSetting<int>
    {
        public OutputChannelSetting(int value, Action<int> onChange = null) : base(value, onChange, localizable: false)
        {
        }

        public override void UpdateValues()
        {
            ResetValues();

            // Add "same as default" option
            _possibleValues.Insert(0, -1);
        }

        public override string ValueToString(int value)
        {
            // Handle "same as default" channel
            if (value == -1)
            {
                return Localize.Key("Settings.Setting.OutputChannels.Default");
            }

            // Channels are paired, if channelIndex == channelCount then it might be a set up such
            // as 4.1 (5 speakers) therefore the final channel will be "5" rather than "5 & 6".
            return value == GlobalAudioHandler.GetOutputChannelCount() ?
                Localize.KeyFormat(
                    "Settings.Setting.OutputChannels.Mono",
                    value
                ) : Localize.KeyFormat(
                    "Settings.Setting.OutputChannels.Stereo",
                    value,
                    value + 1
                );
        }

        protected void ResetValues()
        {
            _possibleValues.Clear();

            // Speakers are paired so increment each speaker by two
            for (int channelIndex = 1; channelIndex < GlobalAudioHandler.GetOutputChannelCount(); channelIndex += 2)
            {
                _possibleValues.Add(channelIndex);
            }
        }
    }
}
