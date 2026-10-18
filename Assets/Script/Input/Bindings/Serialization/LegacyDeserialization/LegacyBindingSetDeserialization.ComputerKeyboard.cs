using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Serialization
{
    public static partial class LegacyBindingSetDeserialization
    {
        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_5F = new("Legacy 5F on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.FiveFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_FRET_GREEN,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_1,   LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_FRET_RED,      new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_2,     LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_FRET_YELLOW,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_3,  LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_FRET_BLUE,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_4,    LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_FRET_ORANGE,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_5,  LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_UP_ARROW,   LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SEMICOLON,     LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_BACKSPACE, LayoutStrings.FIVE_FRET_GUITAR) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_6F = new("Legacy 6F on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.SixFretGuitar)
        {
            Bindings = new()
            {
                { ActionStrings.SIX_FRET_BLACK_1,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_1, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.SIX_FRET_BLACK_2,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_2, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.SIX_FRET_BLACK_3,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_3, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.SIX_FRET_WHITE_1,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Q, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.SIX_FRET_WHITE_2,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_W, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.SIX_FRET_WHITE_3,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_E, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.GUITAR_STRUM_UP,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_UP_ARROW,   LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.GUITAR_STRUM_DOWN,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.GUITAR_WHAMMY,      new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SEMICOLON,     LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.GUITAR_STAR_POWER,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_BACKSPACE, LayoutStrings.FIVE_FRET_GUITAR) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_4L_DRUMS = new("Legacy 4L Drums on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.FourLaneDrums)
        {
            Bindings = new()
            {
                { ActionStrings.FOUR_DRUMS_RED_PAD,         new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Z,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FOUR_DRUMS_YELLOW_PAD,      new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_X,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_PAD,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_C,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_PAD,       new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_V,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_S,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_D,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_F,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.DRUMS_KICK,                 new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SPACE,  LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_5L_DRUMS = new("Legacy 5L Drums on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.FiveLaneDrums)
        {
            Bindings = new()
            {
                { ActionStrings.FIVE_DRUMS_RED_PAD,         new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Z,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_X,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_DRUMS_BLUE_PAD,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_C,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_V,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.FIVE_DRUMS_GREEN_PAD,       new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_B,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.DRUMS_KICK,                 new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SPACE,  LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_MIDI_DRUMKIT = new("Legacy MIDI Drumkit on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.EliteDrums)
        {
            Bindings = new()
            {
                { ActionStrings.ELITE_DRUMS_4L_RED,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Z,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_4L_YTOM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_X,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_4L_BTOM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_C,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_4L_GTOM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_V,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.ELITE_DRUMS_4L_YCYM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_S,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_4L_BCYM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_D,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_4L_GCYM,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_F,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.ELITE_DRUMS_5L_RED,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Z,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_5L_YELLOW,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_X,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_5L_BLUE,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_C,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_5L_ORANGE,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_V,      LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.ELITE_DRUMS_5L_GREEN,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_B,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.DRUMS_KICK,             new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SPACE,  LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_KEYS = new("Legacy Keys on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.ProKeys)
        {
            Bindings = new()
            {
                { ActionStrings.KEYS_PRO_KEY_1,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_Z, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_2,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_S, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_3,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_X, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_4,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_D, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_5,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_C, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.KEYS_PRO_KEY_6,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_V, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_Q, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_7,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_G, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_2, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_8,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_B, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_W, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_9,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_H, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_3, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_10,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_N, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_E, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_11,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_J, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_4, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_12,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_M, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_R, LayoutStrings.COMPUTER_KEYBOARD),
                } } },

                { ActionStrings.KEYS_PRO_KEY_13,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_COMMA, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_T, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_14,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_L, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_6, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_15,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_PERIOD, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_Y, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_16,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_SEMICOLON, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_7, LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_PRO_KEY_17,     new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_SLASH, LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_U, LayoutStrings.COMPUTER_KEYBOARD),
                } } },

                { ActionStrings.KEYS_PRO_KEY_18,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_I, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_19,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_9, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_20,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_O, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_21,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_0, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_22,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_P, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_23,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_MINUS, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_PRO_KEY_24,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_LEFT_BRACKET, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.KEYS_PRO_KEY_25,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_RIGHT_BRACKET, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.KEYS_STAR_POWER,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_BACKSPACE, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_TOUCH_EFFECTS,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_QUOTE, LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.KEYS_FIVE_LANE_OPEN,    new() { Controls = new() {
                    new(ControlStrings.COMPUTER_KEYBOARD_BACK_QUOTE,    LayoutStrings.COMPUTER_KEYBOARD),
                    new(ControlStrings.COMPUTER_KEYBOARD_SPACE,         LayoutStrings.COMPUTER_KEYBOARD),
                } } },
                { ActionStrings.KEYS_FIVE_LANE_GREEN,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_1, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_FIVE_LANE_RED,     new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_2, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_FIVE_LANE_YELLOW,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_3, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_FIVE_LANE_BLUE,    new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_4, LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.KEYS_FIVE_LANE_ORANGE,  new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_5, LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_VOCALS = new("Legacy Vocals with Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.Vocals)
        {
            Bindings = new()
            {
                { ActionStrings.VOCAL_HIT,          new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_BACKSPACE,  LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.VOCAL_STAR_POWER,   new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_SPACE,      LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };

        private static SerializedReusableBindingSet LEGACY_COMPUTER_KEYBOARD_MENU = new("Legacy Menu Navigation on Computer Keyboard", ControllerFamily.ComputerKeyboard, GameMode.Menu)
        {
            Bindings = new()
            {
                { ActionStrings.MENU_START,         new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_ENTER,          LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_SELECT,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_BACKSPACE,      LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.MENU_GREEN,         new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_1,        LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_RED,           new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_2,        LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_YELLOW,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_3,        LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_BLUE,          new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_4,        LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_ORANGE,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DIGIT_5,        LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.MENU_UP,            new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_UP_ARROW,       LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_DOWN,          new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW,     LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_LEFT,          new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_LEFT_ARROW,     LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_RIGHT,         new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_RIGHT_ARROW,    LayoutStrings.COMPUTER_KEYBOARD) } } },

                { ActionStrings.MENU_SEARCH,        new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_TAB,            LayoutStrings.COMPUTER_KEYBOARD) } } },
                { ActionStrings.MENU_SELECT_ARTIST, new() { Controls = new() { new(ControlStrings.COMPUTER_KEYBOARD_F1,             LayoutStrings.COMPUTER_KEYBOARD) } } },
            }
        };
    }
}
