using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class HighwayPresetContainer : CustomContent<HighwayPreset>
    {
        protected override string ContentDirectory => "highwayPresets";

        public override string PresetTypeStringName => "HighwayPreset";

        public override IReadOnlyList<HighwayPreset> DefaultPresets => HighwayPreset.Defaults;
    }
}