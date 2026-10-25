using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Menu.ProfileList;
using YARG.Helpers;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _gamepadFiveFretGuitarGameplay = new()
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

        private static readonly Dictionary<string, ReusableControlBinding> _gamepadFiveFretGuitarMenu = new()
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

        public static readonly Guid GUID_GAMEPAD_AS_5F_GUITAR = new("95bacc56-60f7-4d6f-826a-726b026847d5");
        public static readonly ReusableBindingSet DefaultGamepadFiveFretGuitarGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 5F Guitar",
            GameMode.FiveLaneDrums,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_5F_GUITAR,
            _gamepadFiveFretGuitarGameplay
        );

        public static readonly Guid GUID_GAMEPAD_AS_5F_GUITAR_MENU = new("a1384e03-b351-48f2-bc6f-6c55db5a82b2");
        public static readonly ReusableBindingSet DefaultGamepadFiveFretGuitarMenu = MakeHardcodedBindingSet(
            "Default Gamepad as 5F Guitar Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_5F_GUITAR_MENU,
            _gamepadFiveFretGuitarMenu
        );
    }
}
