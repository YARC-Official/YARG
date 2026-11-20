using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        public static SerializedReusableBindingSet LEGACY_GAMEPAD_5F = new("Legacy 5F on Gamepad", ControllerFamily.Gamepad, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_TRIGGER,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,   LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_TRIGGER,    LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_STICK_X,     LayoutStrings.GAMEPAD) } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_GAMEPAD_VOCALS = new("Legacy Vocals with Gamepad", ControllerFamily.Gamepad, GameMode.Vocals)
        {
            Bindings = new()
            {
                { ActionStrings.VOCAL_HIT,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.VOCAL_STAR_POWER,   new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },
            }
        };


        public static SerializedReusableBindingSet LEGACY_GAMEPAD_MENU = new("Legacy Gamepad Menu Navigation", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,            LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_UP,       LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_UP,             LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_DOWN,     LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_DOWN,           LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_LEFT,     LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_LEFT,           LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_RIGHT,    LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_RIGHT,          LayoutStrings.GAMEPAD),
                } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_SWITCH_GAMEPAD_MENU = new("Legacy Switch Gamepad Menu Navigation", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,            LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_UP,       LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_UP,             LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_DOWN,     LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_DOWN,           LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_LEFT,     LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_LEFT,           LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_STICK_RIGHT,    LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_DPAD_RIGHT,          LayoutStrings.GAMEPAD),
                } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_GAMEPAD_AS_5F = new("Legacy Gamepad as 5F", ControllerFamily.Gamepad, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_RIGHT_STICK_Y,    LayoutStrings.GAMEPAD),
                } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_STICK_X,    LayoutStrings.GAMEPAD) } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_CRKD = new("Legacy CRKD", ControllerFamily.Gamepad, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_TRIGGER,     LayoutStrings.GAMEPAD) } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_CRKD_FW300 = new("Legacy CRKD FW 3.0+", ControllerFamily.Gamepad, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_TRIGGER,     LayoutStrings.GAMEPAD) } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_CRKD_MENU = new("Legacy CRKD Menu", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,            LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_UP,     LayoutStrings.GAMEPAD),
                    new(ControlStrings.JOYSTICK_UP,         LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_DOWN,   LayoutStrings.GAMEPAD),
                    new(ControlStrings.JOYSTICK_DOWN,       LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_LEFT,   LayoutStrings.GAMEPAD),
                    new(ControlStrings.JOYSTICK_LEFT,       LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_RIGHT,  LayoutStrings.GAMEPAD),
                    new(ControlStrings.JOYSTICK_RIGHT,      LayoutStrings.GAMEPAD),
                } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_GAMEPAD_AS_6F = new("Legacy Gamepad as 6F", ControllerFamily.Gamepad, GameMode.SixFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.SIX_FRET_BLACK_1,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.SIX_FRET_BLACK_2,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.SIX_FRET_BLACK_3,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.SIX_FRET_WHITE_1,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.SIX_FRET_WHITE_2,   new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },
                { ActionStrings.SIX_FRET_WHITE_3,   new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,   LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_RIGHT_STICK_Y,    LayoutStrings.GAMEPAD),
                } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_STICK_X,    LayoutStrings.GAMEPAD) } } },
            }
        };

        public static SerializedReusableBindingSet LEGACY_GAMEPAD_AS_5F_MENU = new("Legacy Gamepad as 5F Menu", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,            LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,        LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,       LayoutStrings.GAMEPAD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_WIITAR_THING_DRUMS = new("Legacy Wiitar Thing Drums", ControllerFamily.Gamepad, GameMode.FiveLaneDrums)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_DRUMS_RED_PAD,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL,   new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_DRUMS_BLUE_PAD,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL,   new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,   LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FIVE_DRUMS_GREEN_PAD,       new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },

                { ActionStrings.DRUMS_KICK,                 new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,    LayoutStrings.GAMEPAD) } } },

            }
        };

        private static SerializedReusableBindingSet LEGACY_WIITAR_THING_DRUMS_MENU = new("Legacy Wiitar Thing Drums Menu", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,            LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,           LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_SHOULDER,   LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,  LayoutStrings.GAMEPAD),
                } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,        LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,       LayoutStrings.GAMEPAD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RB4_INSTRUMENT_MAPPER_DRUMS = new("Legacy RB4 Instrument Mapper Drums", ControllerFamily.Gamepad, GameMode.FourLaneDrums)
        {
            Bindings = new()
            {
                { ActionStrings.FOUR_DRUMS_RED_PAD,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FOUR_DRUMS_YELLOW_PAD,      new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_NORTH,         LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_PAD,        new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_WEST,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_PAD,       new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,         LayoutStrings.GAMEPAD) } } },

                { ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,   new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_STICK_PRESS,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,     new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_STICK_PRESS,    LayoutStrings.GAMEPAD) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,    new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,       LayoutStrings.GAMEPAD) } } },

                { ActionStrings.DRUMS_KICK,                 new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_LEFT_SHOULDER,   LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_RIGHT_SHOULDER,  LayoutStrings.GAMEPAD),
                } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_RB4_INSTRUMENT_MAPPER_DRUMS_MENU = new("Legacy RB4 Instrument Mapper Drums Menu", ControllerFamily.Gamepad, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.GAMEPAD_START,                LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.GAMEPAD_SELECT,               LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_SOUTH,         LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.GAMEPAD_BUTTON_EAST,          LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_STICK_PRESS,     LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.GAMEPAD_RIGHT_STICK_PRESS,    LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.GAMEPAD_LEFT_SHOULDER,        LayoutStrings.GAMEPAD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_UP,          LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_BUTTON_NORTH,     LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() {
                    new(ControlStrings.GAMEPAD_DPAD_DOWN,        LayoutStrings.GAMEPAD),
                    new(ControlStrings.GAMEPAD_BUTTON_WEST,      LayoutStrings.GAMEPAD),
                } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_LEFT,        LayoutStrings.GAMEPAD) } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() { new(ControlStrings.GAMEPAD_DPAD_RIGHT,       LayoutStrings.GAMEPAD) } } },
            }
        };
    }
}
