using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private static SerializedReusableBindingSet LEGACY_4L_DRUMKIT = new("Legacy 4L Drumkit", ControllerFamily.FourLaneDrumkit, GameMode.FourLaneDrums)
        {
            Bindings = new()
            {
                { ActionStrings.FOUR_DRUMS_RED_PAD,         new() { Controls = new() { new(ControlStrings.DRUMKIT_RED_PAD,          LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.FOUR_DRUMS_YELLOW_PAD,      new() { Controls = new() { new(ControlStrings.DRUMKIT_YELLOW_PAD,       LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_PAD,        new() { Controls = new() { new(ControlStrings.DRUMKIT_BLUE_PAD,         LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_PAD,       new() { Controls = new() { new(ControlStrings.DRUMKIT_GREEN_PAD,        LayoutStrings.FOUR_LANE_DRUMKIT) } } },

                { ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,   new() { Controls = new() { new(ControlStrings.DRUMKIT_YELLOW_CYMBAL,    LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,     new() { Controls = new() { new(ControlStrings.DRUMKIT_BLUE_CYMBAL,      LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,    new() { Controls = new() { new(ControlStrings.DRUMKIT_GREEN_CYMBAL,     LayoutStrings.FOUR_LANE_DRUMKIT) } } },

                { ActionStrings.DRUMS_KICK,                 new() {Controls = new() {
                    new(ControlStrings.DRUMKIT_4L_KICK_1, LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_4L_KICK_2, LayoutStrings.FOUR_LANE_DRUMKIT),
                } } }
            }
        };

        private static SerializedReusableBindingSet LEGACY_4L_DRUMKIT_MENU = new("Legacy 4L Drumkit Menu", ControllerFamily.FourLaneDrumkit, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,     new() { Controls = new() { new(ControlStrings.PLASTICBAND_START,    LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.MENU_SELECT,    new() { Controls = new() { new(ControlStrings.PLASTICBAND_SELECT,   LayoutStrings.FOUR_LANE_DRUMKIT) } } },

                { ActionStrings.MENU_GREEN,     new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_BUTTON_SOUTH,    LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_GREEN_PAD,       LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_GREEN_CYMBAL,    LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_RED,       new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_BUTTON_EAST,     LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_RED_PAD,         LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_YELLOW,    new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_BUTTON_NORTH,    LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_YELLOW_CYMBAL,   LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_BLUE,      new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_BUTTON_WEST,     LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_BLUE_CYMBAL,     LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_ORANGE,    new() { Controls = new() { new(ControlStrings.DRUMKIT_4L_KICK_1,    LayoutStrings.FOUR_LANE_DRUMKIT) } } },

                { ActionStrings.MENU_UP,        new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_UP,         LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_YELLOW_PAD,      LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_DOWN,      new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_UP,         LayoutStrings.FOUR_LANE_DRUMKIT),
                    new(ControlStrings.DRUMKIT_BLUE_PAD,        LayoutStrings.FOUR_LANE_DRUMKIT),
                } } },
                { ActionStrings.MENU_LEFT,      new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,    LayoutStrings.FOUR_LANE_DRUMKIT) } } },
                { ActionStrings.MENU_RIGHT,     new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,   LayoutStrings.FOUR_LANE_DRUMKIT) } } },
            }
        };
    }
}
