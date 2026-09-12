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

        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.greenPad)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.greenCymbal)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonSouth)),
                    }
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.redPad)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonEast)),
                    }
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.yellowCymbal)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonNorth)),
                    }
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.blueCymbal)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonWest)),
                    }
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.kick1)),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.kick2)),
                    }
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_UP),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.yellowPad)),
                    }
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_DOWN),
                        new (ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.bluePad)),
                    }
                )
            },
            {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_LEFT)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_RIGHT)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitMenuManualOnly = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonSouth))
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonEast))
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonNorth))
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, nameof(FourLaneDrumkit.buttonWest))
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_UP)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_DOWN)
                )
            },
            {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_LEFT)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultFourLaneDrumkit = MakeHardcodedBindingSet(
            "Default 4L Gameplay",
            GameMode.FourLaneDrums,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitDefaults
        );

        public static ReusableBindingSet DefaultFourLaneDrumkitMenu = MakeHardcodedBindingSet(
            "Default 4L Menu",
            GameMode.Menu,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitMenuDefaults
        );

        public static ReusableBindingSet FourLaneDrumkitManualMenu = MakeHardcodedBindingSet(
            "Manual",
            GameMode.Menu,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitMenuManualOnly
        );
    }
}
