using System;
using System.Collections.Generic;
using System.Text;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Menu.ProfileList;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _gamepadFiveFretGuitarGameplay = new()
        {
            {
                ActionStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER)
                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig> {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_SELECT),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_STICK_Y)
                        {
                            PressPoint = 1f
                        },
                    }
                )
            },

            {
                ActionStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_STICK_X)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _gamepadFiveFretGuitarMenu = new()
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
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER)
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

        public static ReusableBindingSet DefaultGamepadFiveFretGuitarGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 5F Guitar",
            GameMode.FiveLaneDrums,
            ControllerFamily.Gamepad,
            _gamepadFiveFretGuitarGameplay
        );

        public static ReusableBindingSet DefaultGamepadFiveFretGuitarMenu = MakeHardcodedBindingSet(
            "Default Gamepad as 5F Guitar Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _gamepadFiveFretGuitarMenu
        );
    }
}
