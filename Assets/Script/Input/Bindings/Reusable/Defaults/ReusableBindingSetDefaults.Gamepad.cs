using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _gamepadMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, "start")
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, "select")
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, nameof(Gamepad.buttonSouth))
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, nameof(Gamepad.buttonEast))
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, nameof(Gamepad.buttonNorth))
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, nameof(Gamepad.buttonWest))
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, nameof(Gamepad.rightShoulder))
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.DPAD_UP),
                        new(ControllerFamily.Gamepad, "leftStick/up"),
                    }
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.DPAD_DOWN),
                        new(ControllerFamily.Gamepad, "leftStick/down"),
                    }
                )
            },
            {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.DPAD_LEFT),
                        new(ControllerFamily.Gamepad, "leftStick/left"),
                    }
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.DPAD_RIGHT),
                        new(ControllerFamily.Gamepad, "leftStick/right"),
                    }
                )
            },
        };

        public static ReusableBindingSet DefaultGamepadMenu = MakeHardcodedBindingSet(
            "Default Gamepad Menu",
            GameMode.Menu,
            ControllerFamily.Gamepad,
            _gamepadMenuDefaults
        );
    }
}
