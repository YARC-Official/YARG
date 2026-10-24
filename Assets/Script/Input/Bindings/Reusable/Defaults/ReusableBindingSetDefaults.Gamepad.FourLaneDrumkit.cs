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
        private static readonly Dictionary<string, ReusableControlBinding> _gamepadFourLaneDrumkitGameplay = new()
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

        private static readonly Dictionary<string, ReusableControlBinding> _gamepadFourLaneDrumkitMenu = new()
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

        public static readonly Guid GUID_GAMEPAD_AS_4L_DRUMKIT = new("dcbc2bf5-99d9-4ea2-ba19-fd90621cb349");
        public static readonly ReusableBindingSet DefaultGamepadFourLaneDrumkitGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 4L Drumkit",
            GameMode.FourLaneDrums,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_4L_DRUMKIT,
            _gamepadFourLaneDrumkitGameplay
        );

        public static readonly Guid GUID_GAMEPAD_AS_4L_DRUMKIT_MENU = new("88a2f7f5-a6b0-45cf-812f-0b0cad9fba46");
        public static readonly ReusableBindingSet DefaultGamepadFourLaneDrumkitMenu = MakeHardcodedBindingSet(
            "Default Gamepad as 4L Drumkit Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_4L_DRUMKIT_MENU,
            _gamepadFourLaneDrumkitMenu
        );
    }
}
