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

        private static Dictionary<string, ReusableControlBinding> _fiveLaneDrumkitMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.greenPad)),
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonSouth)),
                    }
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.redPad)),
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonEast)),
                    }
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.yellowCymbal)),
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonNorth)),
                    }
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.bluePad)),
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonWest)),
                    }
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.orangeCymbal)),
                        new (ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.kick)),
                    }
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

        private static Dictionary<string, ReusableControlBinding> _fiveLaneDrumkitMenuManualOnly = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonSouth))
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonEast))
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonNorth))
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, nameof(FiveLaneDrumkit.buttonWest))
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.DPAD_UP)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.DPAD_DOWN)
                )
            },
            {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.DPAD_LEFT)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultFiveLaneDrumkit = MakeHardcodedBindingSet(
            "Default 5L Drums Gameplay",
            GameMode.FiveLaneDrums,
            ControllerFamily.FiveLaneDrumkit,
            _fiveLaneDrumkitDefaults
        );

        public static ReusableBindingSet DefaultFiveLaneDrumkitMenu = MakeHardcodedBindingSet(
            "Default 5L Drums Menu",
            GameMode.Menu,
            ControllerFamily.FiveLaneDrumkit,
            _fiveLaneDrumkitMenuDefaults
        );

        public static ReusableBindingSet FiveLaneDrumkitControllerOnlyMenu = MakeHardcodedBindingSet(
            "D-Pad and Face Buttons Only",
            GameMode.Menu,
            ControllerFamily.FiveLaneDrumkit,
            _fiveLaneDrumkitMenuManualOnly
        );
    }
}
