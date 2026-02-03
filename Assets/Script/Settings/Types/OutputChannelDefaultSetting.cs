using System;
using YARG.Core.Audio;
using YARG.Localization;

namespace YARG.Settings.Types
{
    public class OutputChannelDefaultSetting : OutputChannelSetting
    {
        public OutputChannelDefaultSetting(int value, Action<int> onChange = null) : base(value, onChange)
        {
        }

        public override void UpdateValues()
        {
            ResetValues();
        }
    }
}
