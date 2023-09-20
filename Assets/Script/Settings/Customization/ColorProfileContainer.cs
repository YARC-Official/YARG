using System;
using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class ColorProfileContainer : CustomContent<ColorProfile>
    {
        private static readonly List<ColorProfile> _defaultProfiles = new() { ColorProfile.Default };
        public override IReadOnlyList<ColorProfile> DefaultPresets => _defaultProfiles;

        public ColorProfileContainer(string contentDirectory) : base(contentDirectory)
        {
        }

        public override void SetSettingsFromPreset(BasePreset preset)
        {
            if (preset is not ColorProfile p)
            {
                throw new InvalidOperationException("Invalid preset type!");
            }

            SettingsManager.Settings.ColorProfile_Ref = p;
        }

        public override void SetPresetFromSettings(BasePreset preset)
        {
            // This is not needed since color profile is a reference type
        }
    }
}