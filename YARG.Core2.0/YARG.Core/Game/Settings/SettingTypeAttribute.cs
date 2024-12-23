using System;

namespace YARG.Core.Game.Settings
{
    public enum SettingType
    {
        Special,

        Input,
        MillisecondInput,

        Slider,

        Toggle
    }

    public class SettingTypeAttribute : Attribute
    {
        public SettingType Type { get; }

        public SettingTypeAttribute(SettingType type)
        {
            Type = type;
        }
    }
}