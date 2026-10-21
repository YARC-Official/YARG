using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private static SerializedReusableBindingSet LEGACY_PRO_KEYBOARD = new("Legacy Pro Keyboard", ControllerFamily.ProKeyboard, GameMode.ProKeys)
        {
            Bindings = new()
            {
                { ActionStrings.KEYS_PRO_KEY_1,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_1,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_2,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_2,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_3,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_3,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_4,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_4,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_5,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_5,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_6,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_6,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_7,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_7,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_8,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_8,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_9,     new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_9,   LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_10,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_10,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_11,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_11,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_12,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_12,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_13,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_13,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_14,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_14,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_15,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_15,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_16,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_16,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_17,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_17,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_18,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_18,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_19,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_19,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_20,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_20,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_21,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_21,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_22,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_22,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_23,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_23,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_24,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_24,  LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_25,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_KEY_25,  LayoutStrings.PRO_KEYBOARD) } } },

                { ActionStrings.KEYS_STAR_POWER,    new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT,          LayoutStrings.PRO_KEYBOARD),
                    new(ControlStrings.PRO_KEYBOARD_OVERDRIVE,      LayoutStrings.PRO_KEYBOARD),
                    new(ControlStrings.PRO_KEYBOARD_DIGITAL_PEDAL,  LayoutStrings.PRO_KEYBOARD),
                } } },

                { ActionStrings.KEYS_TOUCH_EFFECTS, new() { Controls = new() {
                    new(ControlStrings.PRO_KEYBOARD_TOUCH_STRIP,    LayoutStrings.PRO_KEYBOARD),
                    new(ControlStrings.PRO_KEYBOARD_ANALOG_PEDAL,    LayoutStrings.PRO_KEYBOARD),
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_PRO_KEYBOARD_MENU = new("Legacy Pro Keyboard Menu", ControllerFamily.ProKeyboard, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START,        LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT,       LayoutStrings.PRO_KEYBOARD) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() { new(ControlStrings.PRO_KEYBOARD_OVERDRIVE,   LayoutStrings.PRO_KEYBOARD) } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,        LayoutStrings.PRO_KEYBOARD) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,       LayoutStrings.PRO_KEYBOARD) } } },
            }
        };
    }
}
