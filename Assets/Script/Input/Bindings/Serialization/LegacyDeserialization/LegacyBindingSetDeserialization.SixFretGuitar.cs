using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private static SerializedReusableBindingSet LEGACY_6F_GUITAR = new("Legacy 6F Guitar", ControllerFamily.FiveFretGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.SIX_FRET_BLACK_1,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_1, LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.SIX_FRET_BLACK_2,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_2, LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.SIX_FRET_BLACK_3,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_3, LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.SIX_FRET_WHITE_1,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_WHITE_1, LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.SIX_FRET_WHITE_2,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_WHITE_2, LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.SIX_FRET_WHITE_3,   new() { Controls = new() { new(ControlStrings.GUITAR_6F_WHITE_3, LayoutStrings.SIX_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_UP,   LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GUITAR_STRUM_DOWN, LayoutStrings.SIX_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GUITAR_WHAMMY,     LayoutStrings.SIX_FRET_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.FIVE_FRET_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.FIVE_FRET_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_6F_GUITAR_MENU = new("Legacy 6F Guitar Menu", ControllerFamily.FiveFretGuitar, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT,   LayoutStrings.SIX_FRET_GUITAR) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_1,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_2,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() { new(ControlStrings.GUITAR_6F_BLACK_3,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() { new(ControlStrings.GUITAR_6F_WHITE_1,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() { new(ControlStrings.GUITAR_6F_WHITE_2,    LayoutStrings.SIX_FRET_GUITAR) } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,      LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,    LayoutStrings.SIX_FRET_GUITAR) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,   LayoutStrings.SIX_FRET_GUITAR) } } },
            }
        };
    }
}
