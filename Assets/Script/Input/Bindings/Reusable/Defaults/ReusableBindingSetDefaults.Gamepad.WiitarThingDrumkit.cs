using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _defaultWiitarGuitarDrumkitGameplay = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_LEFT_SHOULDER),
                    }
                )
            },

            {
                ActionStrings.FIVE_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_RIGHT_SHOULDER)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveLaneDrumkit, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _defaultWiitarThingDrumkitMenu = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultWiitarThingDrumkitGameplay = MakeHardcodedBindingSet(
            "Default Wiitar Thing Guitar",
            GameMode.FiveLaneDrums,
            ControllerFamily.Gamepad,
            _defaultWiitarGuitarDrumkitGameplay
        );

        public static ReusableBindingSet DefaultWiitarThingDrumkitMenu = MakeHardcodedBindingSet(
            "Default Wiitar Thing Guitar Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _defaultWiitarThingDrumkitMenu
        );
    }
}
