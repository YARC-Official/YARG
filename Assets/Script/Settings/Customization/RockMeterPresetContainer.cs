using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class RockMeterPresetContainer : CustomContent<RockMeterPreset>
    {
        protected override string ContentDirectory => "rockMeterPresets";

        public override string PresetTypeStringName => "RockMeterPreset";

        public override IReadOnlyList<RockMeterPreset> DefaultPresets => RockMeterPreset.Defaults;
    }
}