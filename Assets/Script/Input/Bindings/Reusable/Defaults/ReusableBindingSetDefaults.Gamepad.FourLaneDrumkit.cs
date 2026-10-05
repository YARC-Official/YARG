using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _gamepadFourLaneDrumkitGameplay = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_TRIGGER),
                    }
                )
            },

            {
                ActionStrings.FOUR_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_YELLOW_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },

            {
                ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_PRESS)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_STICK_PRESS)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _gamepadFourLaneDrumkitMenu = new()
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
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER), // Green cymbal
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH),
                    }
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
                    new List<ReusableSingleButtonBindingConfig> {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_PRESS), // Yellow cymbal
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH),
                    }
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_STICK_PRESS), // Blue cymbal
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST),
                    }
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_TRIGGER),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP),
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH),
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN),
                        new (ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST),
                    }
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

        public static ReusableBindingSet GamepadFourLaneDrumkitGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 4L Drumkit",
            GameMode.FourLaneDrums,
            ControllerFamily.Gamepad,
            _gamepadFourLaneDrumkitGameplay
        );

        public static ReusableBindingSet GamepadFourLaneDrumkitMenu = MakeHardcodedBindingSet(
            "Default Gamepad as 4L Drumkit Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _gamepadFourLaneDrumkitMenu
        );
    }
}
