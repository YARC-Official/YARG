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
        private static Dictionary<string, ReusableControlBinding> _gamepadSixFretGuitarGameplay = new()
        {
            {
                ActionStrings.SIX_FRET_BLACK_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.SIX_FRET_BLACK_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.SIX_FRET_BLACK_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER)                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_SELECT)
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

        private static Dictionary<string, ReusableControlBinding> _gamepadSixFretGuitarMenu = new()
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
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultGamepadSixFretGuitarGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 6F Guitar",
            GameMode.SixFretGuitar,
            ControllerFamily.Gamepad,
            _gamepadSixFretGuitarGameplay
        );

        public static ReusableBindingSet DefaultGamepadSixFretGuitarMenu = MakeHardcodedBindingSet(
            "Default Gamepad as 6F Guitar Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _gamepadSixFretGuitarMenu
        );
    }
}
