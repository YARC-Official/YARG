using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _gamepadKeysGameplay = new()
        {
            {
                ActionStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_LEFT_SHOULDER)
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_GREEN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_UP),
                        new(ControllerFamily.Gamepad, ControlStrings.GAMEPAD_DPAD_DOWN),
                    }
                )
            },
        };

        public static readonly Guid GUID_GAMEPAD_AS_KEYBOARD = new("42f1af1c-e685-400d-b6f9-875ccfa88c59");
        public static readonly ReusableBindingSet DefaultGamepadKeysGameplay = MakeHardcodedBindingSet(
            "Default Gamepad as 5L Keyboard",
            GameMode.ProKeys,
            ControllerFamily.Gamepad,
            GUID_GAMEPAD_AS_KEYBOARD,
            _gamepadKeysGameplay
        );
    }
}
