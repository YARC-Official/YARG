using System.Collections.Generic;
using YARG.Core.Game;

namespace YARG.Settings.Customization
{
    public class CameraSettingsContainer : CustomContent<CameraPreset>
    {
        protected override string ContentDirectory => "cameras";

        public override string PresetTypeStringName => "CameraPreset";

        public override IReadOnlyList<CameraPreset> DefaultPresets => CameraPreset.Defaults;
    }
}