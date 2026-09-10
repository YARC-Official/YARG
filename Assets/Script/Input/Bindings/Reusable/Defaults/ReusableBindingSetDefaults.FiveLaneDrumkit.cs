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
        private static Dictionary<string, ReusableControlBinding> _fiveLaneDrumkitDefaults = new()
        {
            {
                ControlStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.kick)),
                    }
                )
            },

            {
                ControlStrings.FIVE_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.FIVE_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.redPad))
                )
            },

            {
                ControlStrings.FIVE_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.FIVE_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.yellowCymbal))
                )
            },

            {
                ControlStrings.FIVE_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.FIVE_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.bluePad))
                )
            },

            {
                ControlStrings.FIVE_DRUMS_ORANGE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.FIVE_DRUMS_ORANGE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.orangeCymbal))
                )
            },

            {
                ControlStrings.FIVE_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ControlStrings.FIVE_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.greenPad))
                )
            },
        };

        public static ReusableBindingSet DefaultFiveLaneDrumkit = MakeHardcodedBindingSet(
            "Default 5-Lane Drumkit",
            GameMode.FiveLaneDrums,
            ControllerFamily.FiveLaneDrumkit,
            _fiveLaneDrumkitDefaults,
            ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT
        );
    }
}
