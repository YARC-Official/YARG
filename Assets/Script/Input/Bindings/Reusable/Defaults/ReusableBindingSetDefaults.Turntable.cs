using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _turntableMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Turntable, ControlStrings.PLASTICBAND_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Turntable, ControlStrings.PLASTICBAND_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Turntable, ControlStrings.GAMEPAD_BUTTON_SOUTH),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_LEFT_GREEN),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_RIGHT_GREEN),
                    }
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Turntable, ControlStrings.GAMEPAD_BUTTON_EAST),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_LEFT_RED),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_RIGHT_RED),
                    }
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Turntable, ControlStrings.TURNTABLE_EUPHORIA)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Turntable, ControlStrings.GAMEPAD_BUTTON_WEST),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_LEFT_BLUE),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_RIGHT_BLUE),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Turntable, ControlStrings.GAMEPAD_DPAD_UP),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_LEFT_VELOCITY),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_RIGHT_VELOCITY),
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Turntable, ControlStrings.GAMEPAD_DPAD_DOWN),
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_LEFT_VELOCITY)
                        {
                            PressPoint = -ReusableSingleButtonBinding.PRESS_POINT_DEFAULT,
                            Inverted = !ReusableSingleButtonBinding.INVERTED_DEFAULT
                        },
                        new(ControllerFamily.Turntable, ControlStrings.TURNTABLE_RIGHT_VELOCITY)
                        {
                            PressPoint = -ReusableSingleButtonBinding.PRESS_POINT_DEFAULT,
                            Inverted = !ReusableSingleButtonBinding.INVERTED_DEFAULT
                        }
                    }
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig (ControllerFamily.Turntable, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Turntable, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static readonly Guid GUID_TURNTABLE_MENU = new("6601f8b8-9167-4fc0-a749-ff547c279eb7");
        public static readonly ReusableBindingSet DefaultTurntableMenu = MakeHardcodedBindingSet(
            "Default Turntable Menu",
            GameMode.Menu,
            ControllerFamily.Turntable,
            GUID_TURNTABLE_MENU,
            _turntableMenuDefaults
        );
    }
}
