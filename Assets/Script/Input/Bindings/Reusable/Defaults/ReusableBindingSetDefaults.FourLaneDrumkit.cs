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
                ControlStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.kick1)),
                        new(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.kick2)),
                    }
                )
            },

            {
                ControlStrings.FOUR_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.redPad))
                )
            },
            {
                ControlStrings.FOUR_DRUMS_YELLOW_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_YELLOW_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.yellowPad))
                )
            },
            {
                ControlStrings.FOUR_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.bluePad))
                )
            },
            {
                ControlStrings.FOUR_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.greenPad))
                )
            },

            {
                ControlStrings.FOUR_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.yellowCymbal))
                )
            },
            {
                ControlStrings.FOUR_DRUMS_BLUE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_BLUE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.blueCymbal))
                )
            },
            {
                ControlStrings.FOUR_DRUMS_GREEN_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ControlStrings.FOUR_DRUMS_GREEN_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.greenCymbal))
                )
            },
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
