using System.Collections.Generic;

namespace YARG.Settings.Customization
{
    public class EnginePresetContainer : CustomContent<EnginePreset>
    {
        protected override string ContentDirectory => "enginePresets";

        public override string PresetTypeStringName => "EnginePreset";

        public override IReadOnlyList<EnginePreset> DefaultPresets => EnginePreset.Defaults;
    }
}