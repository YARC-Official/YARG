using System.Collections.Generic;
using YARG.Themes;

namespace YARG.Settings.Customization
{
    public class ThemePresetContainer : CustomContent<ThemePreset>
    {
        public override IReadOnlyList<ThemePreset> DefaultPresets => ThemePreset.Defaults;

        public override string PresetTypeStringName => "ThemePreset";

        public ThemePresetContainer(string contentDirectory) : base(contentDirectory)
        {
        }
    }
}