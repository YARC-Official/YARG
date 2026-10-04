using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _gamepadMenuDefaults = new()
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
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_RIGHT_SHOULDER)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_UP),
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_DOWN),
                    }
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_LEFT),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_LEFT),
                    }
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_RIGHT),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_STICK_RIGHT),
                    }
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _gamepadVocalDefaults = new()
        {
            {
                ActionStrings.VOCAL_HIT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_HIT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.VOCAL_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_SELECT)
                )
            },
        };

        public static ReusableBindingSet DefaultGamepadMenu = MakeHardcodedBindingSet(
            "Default Gamepad Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _gamepadMenuDefaults
        );

        public static ReusableBindingSet DefaultGamepadVocalGameplay = MakeHardcodedBindingSet(
            "Default Vocals with Gamepad",
            GameMode.Vocals,
            ControllerFamily.Gamepad,
            _gamepadVocalDefaults
        );
    }
}
