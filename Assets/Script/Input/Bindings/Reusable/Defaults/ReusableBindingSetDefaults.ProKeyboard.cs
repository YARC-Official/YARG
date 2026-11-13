using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _proKeyboardDefaults = new()
        {
            {
                ActionStrings.KEYS_PRO_KEY_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_1)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_2)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_3)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_4,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_4],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_4)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_5,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_5],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_5)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_6,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_6],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_6)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_7,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_7],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_7)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_8,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_8],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_8)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_9,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_9],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_9)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_10,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_10],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_10)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_11,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_11],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_11)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_12,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_12],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_12)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_13,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_13],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_13)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_14,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_14],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_14)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_15,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_15],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_15)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_16,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_16],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_16)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_17,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_17],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_17)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_18,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_18],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_18)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_19,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_19],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_19)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_20,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_20],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_20)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_21,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_21],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_21)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_22,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_22],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_22)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_23,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_23],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_23)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_24,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_24],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_24)
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_25,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_25],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_25)
                )
            },

            {
                ActionStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_OPEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_10),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_12),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_22),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_24),
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_GREEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_1),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_13),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_25)
                    }
                    
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_RED],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_3),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_15)
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_5),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_17)
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_BLUE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_6),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_18)
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_8),
                        new (ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_KEY_20)
                    }

                )
            },

            {
                ActionStrings.KEYS_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ProKeyboard, ControlStrings.PLASTICBAND_SELECT),
                        new(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_OVERDRIVE),
                        new(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_DIGITAL_PEDAL)
                    }
                )
            },
            {
                ActionStrings.KEYS_TOUCH_EFFECTS,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_TOUCH_EFFECTS],
                    new List<ReusableSingleAxisBindingConfig>() {
                        new(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_TOUCH_STRIP),
                        new(ControllerFamily.ProKeyboard, ControlStrings.PRO_KEYBOARD_ANALOG_PEDAL),
                    }
                )
            },
        };

        private static readonly Dictionary<string, ReusableControlBinding> _proKeyboardDefaultMenu = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PLASTICBAND_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.PLASTICBAND_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_BUTTON_SOUTH)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_BUTTON_EAST)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_BUTTON_NORTH)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_BUTTON_WEST)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static readonly Guid GUID_PRO_KEYBOARD = new("420f5e05-ed3e-4113-aeff-c75033a6888c");
        public static readonly ReusableBindingSet DefaultProKeyboard = MakeHardcodedBindingSet(
            "Default Keys",
            GameMode.ProKeys,
            ControllerFamily.ProKeyboard,
            GUID_PRO_KEYBOARD,
            _proKeyboardDefaults
        );

        public static readonly Guid GUID_PRO_KEYBOARD_MENU = new("a856cc8b-2413-4f9f-8d3a-c02f98768ce8");
        public static readonly ReusableBindingSet DefaultProKeyboardMenu = MakeHardcodedBindingSet(
            "Default Pro Keyboard Menu",
            GameMode.Menu,
            ControllerFamily.ProKeyboard,
            GUID_PRO_KEYBOARD_MENU,
            _proKeyboardDefaultMenu
        );
    }
}