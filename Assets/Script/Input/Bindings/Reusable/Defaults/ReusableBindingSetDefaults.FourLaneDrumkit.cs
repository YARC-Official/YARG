using PlasticBand.Devices;
using System.Collections.Generic;
using YARG.Helpers;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitDefaults = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_4L_KICK_1),
                        new(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_4L_KICK_2),
                    }
                )
            },

            {
                ActionStrings.FOUR_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_RED_PAD)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_YELLOW_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_YELLOW_PAD)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_BLUE_PAD)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_GREEN_PAD)
                )
            },

            {
                ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_YELLOW_CYMBAL)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_BLUE_CYMBAL)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_GREEN_CYMBAL)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.PLASTICBAND_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.PLASTICBAND_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_GREEN_PAD),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_GREEN_CYMBAL),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_SOUTH),
                    }
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_RED_PAD),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_EAST),
                    }
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_YELLOW_CYMBAL),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_NORTH),
                    }
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_BLUE_CYMBAL),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_WEST),
                    }
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_4L_KICK_1),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_4L_KICK_2),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_UP),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_YELLOW_PAD),
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_DOWN),
                        new (ControllerFamily.FourLaneDrumkit, ControlStrings.DRUMKIT_BLUE_PAD),
                    }
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _fourLaneDrumkitMenuManualOnly = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.PLASTICBAND_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.PLASTICBAND_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FourLaneDrumkit, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultFourLaneDrumkit = MakeHardcodedBindingSet(
            "Default 4L Drums",
            GameMode.FourLaneDrums,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitDefaults
        );

        public static ReusableBindingSet DefaultFourLaneDrumkitMenu = MakeHardcodedBindingSet(
            "Default 4L Drumkit Menu",
            GameMode.Menu,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitMenuDefaults
        );

        public static ReusableBindingSet FourLaneDrumkitControllerOnlyMenu = MakeHardcodedBindingSet(
            "D-Pad and Face Buttons Only",
            GameMode.Menu,
            ControllerFamily.FourLaneDrumkit,
            _fourLaneDrumkitMenuManualOnly
        );
    }
}
