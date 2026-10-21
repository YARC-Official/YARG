using PlasticBand.Devices;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private static SerializedReusableBindingSet LEGACY_5F_ON_PRO_GUITAR = new("Legacy 5F on Pro Guitar", ControllerFamily.ProGuitar, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_GREEN_FRET,     LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GUITAR_5F_RED_FRET,       LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_YELLOW_FRET,    LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GUITAR_5F_BLUE_FRET,      LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,    LayoutStrings.PRO_GUITAR) } } },

                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.PRO_GUITAR_STRUM_1, LayoutStrings.PRO_GUITAR) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.PRO_GUITAR_DIGITAL_PEDAL, LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.GUITAR_TILT, LayoutStrings.PRO_GUITAR) { Parameters = new() { { "PressPoint", DEFAULT_LEGACY_TILT_PRESS_POINT } } },
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_PRO_GUITAR_MENU = new("Legacy Pro Guitar Menu", ControllerFamily.ProGuitar, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START, LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT, LayoutStrings.PRO_GUITAR) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_GREEN_FRET,   LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.GAMEPAD_BUTTON_SOUTH, LayoutStrings.PRO_GUITAR),
                } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_RED_FRET,     LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.GAMEPAD_BUTTON_EAST, LayoutStrings.PRO_GUITAR),
                } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_YELLOW_FRET,  LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.GAMEPAD_BUTTON_NORTH, LayoutStrings.PRO_GUITAR),
                } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() {
                    new(ControlStrings.GUITAR_5F_BLUE_FRET,    LayoutStrings.PRO_GUITAR),
                    new(ControlStrings.GAMEPAD_BUTTON_WEST, LayoutStrings.PRO_GUITAR),
                } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() { new(ControlStrings.GUITAR_5F_ORANGE_FRET,  LayoutStrings.PRO_GUITAR) } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP, LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN, LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT, LayoutStrings.PRO_GUITAR) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT, LayoutStrings.PRO_GUITAR) } } },
            }
        };
    }
}
