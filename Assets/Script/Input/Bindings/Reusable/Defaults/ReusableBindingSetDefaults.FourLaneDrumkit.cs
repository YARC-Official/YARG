using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitDefaults = new()
        {
            {
                ControlStrings.FOUR_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(nameof(FourLaneDrumkit.redPad), LayoutStrings.FOUR_LANE_DRUMKIT)
                )
            }
        };

        public static ReusableBindingSet DefaultFourLaneDrumkit = MakeHardcodedBindingSet(
            "Default 4-Lane Drumkit",
            GameMode.FourLaneDrums,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitDefaults,
            ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT
        );
    }
}
