using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private const string DEFAULT_LEGACY_TILT_PRESS_POINT = "1.0";
        private const string DEFAULT_LEGACY_RIFFMASTER_TILT_PRESS_POINT = "0.7";

        private static SerializedReusableBindingSet LEGACY_5F_GUITAR = new("Legacy Generic 5F Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RB_GUITAR = new("Legacy RB Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.FIVE_FRET_SOLO_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_GH_GUITAR = new("Legacy GH 5F Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.FIVE_FRET_SOLO_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_GREEN,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_RED,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_YELLOW,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_BLUE,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_TOUCH_ORANGE,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_SP_PEDAL, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RIFFMASTER = new("Legacy Riffmaster", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.FIVE_FRET_SOLO_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.FIVE_FRET_SOLO_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_RIFFMASTER_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_5F_GUITAR_MENU = new("Legacy Generic 5F Guitar Menu", ControllerFamily.FiveFretGuitar, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT, LayoutStrings.FIVE_FRET_GUITAR) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RB_GUITAR_MENU = new("Legacy RB 5F Guitar Menu", ControllerFamily.FiveFretGuitar, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT, LayoutStrings.FIVE_FRET_GUITAR) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RIFFMASTER_MENU = new("Legacy Riffmaster Menu", ControllerFamily.FiveFretGuitar, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START, LayoutStrings.FIVE_FRET_GUITAR) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_GREEN_FRET,   LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_RED_FRET,     LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_BLUE_FRET,    LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET,  LayoutStrings.FIVE_FRET_GUITAR),
                } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_UP, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.JOYSTICK_UP, LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_DOWN, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.JOYSTICK_DOWN, LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_LEFT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.JOYSTICK_LEFT, LayoutStrings.FIVE_FRET_GUITAR),
                } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_RIGHT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.JOYSTICK_RIGHT, LayoutStrings.FIVE_FRET_GUITAR),
                } } },
            }
        };
    }
}
