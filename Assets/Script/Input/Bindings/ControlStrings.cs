using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{
    public static class ControlStrings
    {
        public const string MENU_START = "Menu.Start";
        public const string MENU_SELECT = "Menu.Select";
        public const string MENU_GREEN = "Menu.Green";
        public const string MENU_RED = "Menu.Red";
        public const string MENU_YELLOW = "Menu.Yellow";
        public const string MENU_BLUE = "Menu.Blue";
        public const string MENU_ORANGE = "Menu.Orange";
        public const string MENU_UP = "Menu.Up";
        public const string MENU_DOWN = "Menu.Down";
        public const string MENU_LEFT = "Menu.Left";
        public const string MENU_RIGHT = "Menu.Right";
        public const string MENU_SEARCH = "Menu.Search";
        public const string MENU_SELECT_ARTIST = "Menu.SelectArtist";

        public const string GUITAR_STRUM_UP = "Guitar.StrumUp";
        public const string GUITAR_STRUM_DOWN = "Guitar.StrumDown";
        public const string GUITAR_STAR_POWER = "Guitar.StarPower";
        public const string GUITAR_WHAMMY = "Guitar.Whammy";

        public const string FIVE_FRET_GREEN = "FiveFret.Green";
        public const string FIVE_FRET_RED = "FiveFret.Red";
        public const string FIVE_FRET_YELLOW = "FiveFret.Yellow";
        public const string FIVE_FRET_BLUE = "FiveFret.Blue";
        public const string FIVE_FRET_ORANGE = "FiveFret.Orange";
        public const string FIVE_FRET_SOLO_GREEN = "FiveFret.SoloGreen";
        public const string FIVE_FRET_SOLO_RED = "FiveFret.SoloRed";
        public const string FIVE_FRET_SOLO_YELLOW = "FiveFret.SoloYellow";
        public const string FIVE_FRET_SOLO_BLUE = "FiveFret.SoloBlue";
        public const string FIVE_FRET_SOLO_ORANGE = "FiveFret.SoloOrange";

        public const string SIX_FRET_BLACK_1 = "SixFret.Black1";
        public const string SIX_FRET_BLACK_2 = "SixFret.Black2";
        public const string SIX_FRET_BLACK_3 = "SixFret.Black3";
        public const string SIX_FRET_WHITE_1 = "SixFret.White1";
        public const string SIX_FRET_WHITE_2 = "SixFret.White2";
        public const string SIX_FRET_WHITE_3 = "SixFret.White3";


        public const string DRUMS_KICK = "Drums.Kick";

        public const string FOUR_DRUMS_RED_PAD = "FourDrums.RedPad";
        public const string FOUR_DRUMS_YELLOW_PAD = "FourDrums.YellowPad";
        public const string FOUR_DRUMS_BLUE_PAD = "FourDrums.BluePad";
        public const string FOUR_DRUMS_GREEN_PAD = "FourDrums.GreenPad";
        public const string FOUR_DRUMS_YELLOW_CYMBAL = "FourDrums.YellowCymbal";
        public const string FOUR_DRUMS_BLUE_CYMBAL = "FourDrums.BlueCymbal";
        public const string FOUR_DRUMS_GREEN_CYMBAL = "FourDrums.GreenCymbal";

        public const string FIVE_DRUMS_RED_PAD = "FiveDrums.RedPad";
        public const string FIVE_DRUMS_YELLOW_CYMBAL = "FiveDrums.YellowCymbal";
        public const string FIVE_DRUMS_BLUE_PAD = "FiveDrums.BluePad";
        public const string FIVE_DRUMS_ORANGE_CYMBAL = "FiveDrums.OrangeCymbal";
        public const string FIVE_DRUMS_GREEN_PAD = "FiveDrums.GreenPad";


        public const string ELITE_DRUMS_STOMP = "EliteDrums.Stomp";
        public const string ELITE_DRUMS_SPLASH = "EliteDrums.Splash";
        public const string ELITE_DRUMS_SNARE = "EliteDrums.Snare";
        public const string ELITE_DRUMS_CLOSED_HI_HAT = "EliteDrums.ClosedHiHat";
        public const string ELITE_DRUMS_SIZZLE_HI_HAT = "EliteDrums.SizzleHiHat";
        public const string ELITE_DRUMS_OPEN_HI_HAT = "EliteDrums.OpenHiHat";
        public const string ELITE_DRUMS_LEFT_CRASH = "EliteDrums.LeftCrash";
        public const string ELITE_DRUMS_TOM_1 = "EliteDrums.Tom1";
        public const string ELITE_DRUMS_TOM_2 = "EliteDrums.Tom2";
        public const string ELITE_DRUMS_TOM_3 = "EliteDrums.Tom3";
        public const string ELITE_DRUMS_RIDE = "EliteDrums.Ride";
        public const string ELITE_DRUMS_RIGHT_CRASH = "EliteDrums.RightCrash";
        public const string ELITE_DRUMS_4L_RED = "EliteDrums.FourLaneRedDrum";
        public const string ELITE_DRUMS_4L_YTOM = "EliteDrums.FourLaneYellowDrum";
        public const string ELITE_DRUMS_4L_BTOM = "EliteDrums.FourLaneBlueDrum";
        public const string ELITE_DRUMS_4L_GTOM = "EliteDrums.FourLaneGreenDrum";
        public const string ELITE_DRUMS_4L_YCYM = "EliteDrums.FourLaneYellowCymbal";
        public const string ELITE_DRUMS_4L_BCYM = "EliteDrums.FourLaneBlueCymbal";
        public const string ELITE_DRUMS_4L_GCYM = "EliteDrums.FourLaneGreenCymbal";
        public const string ELITE_DRUMS_5L_RED = "EliteDrums.FiveLaneRedDrum";
        public const string ELITE_DRUMS_5L_BLUE = "EliteDrums.FiveLaneBlueDrum";
        public const string ELITE_DRUMS_5L_GREEN = "EliteDrums.FiveLaneGreenDrum";
        public const string ELITE_DRUMS_5L_YELLOW = "EliteDrums.FiveLaneYellowCymbal";
        public const string ELITE_DRUMS_5L_ORANGE = "EliteDrums.FiveLaneOrangeCymbal";



        public const string KEYS_PRO_KEY_1 = "ProKeys.Key1";
        public const string KEYS_PRO_KEY_2 = "ProKeys.Key2";
        public const string KEYS_PRO_KEY_3 = "ProKeys.Key3";
        public const string KEYS_PRO_KEY_4 = "ProKeys.Key4";
        public const string KEYS_PRO_KEY_5 = "ProKeys.Key5";
        public const string KEYS_PRO_KEY_6 = "ProKeys.Key6";
        public const string KEYS_PRO_KEY_7 = "ProKeys.Key7";
        public const string KEYS_PRO_KEY_8 = "ProKeys.Key8";
        public const string KEYS_PRO_KEY_9 = "ProKeys.Key9";
        public const string KEYS_PRO_KEY_10 = "ProKeys.Key10";
        public const string KEYS_PRO_KEY_11 = "ProKeys.Key11";
        public const string KEYS_PRO_KEY_12 = "ProKeys.Key12";
        public const string KEYS_PRO_KEY_13 = "ProKeys.Key13";
        public const string KEYS_PRO_KEY_14 = "ProKeys.Key14";
        public const string KEYS_PRO_KEY_15 = "ProKeys.Key15";
        public const string KEYS_PRO_KEY_16 = "ProKeys.Key16";
        public const string KEYS_PRO_KEY_17 = "ProKeys.Key17";
        public const string KEYS_PRO_KEY_18 = "ProKeys.Key18";
        public const string KEYS_PRO_KEY_19 = "ProKeys.Key19";
        public const string KEYS_PRO_KEY_20 = "ProKeys.Key20";
        public const string KEYS_PRO_KEY_21 = "ProKeys.Key21";
        public const string KEYS_PRO_KEY_22 = "ProKeys.Key22";
        public const string KEYS_PRO_KEY_23 = "ProKeys.Key23";
        public const string KEYS_PRO_KEY_24 = "ProKeys.Key24";
        public const string KEYS_PRO_KEY_25 = "ProKeys.Key25";
        public const string KEYS_FIVE_LANE_OPEN = "ProKeys.OpenNote";
        public const string KEYS_FIVE_LANE_GREEN = "ProKeys.GreenKey";
        public const string KEYS_FIVE_LANE_RED = "ProKeys.RedKey";
        public const string KEYS_FIVE_LANE_YELLOW = "ProKeys.YellowKey";
        public const string KEYS_FIVE_LANE_BLUE = "ProKeys.BlueKey";
        public const string KEYS_FIVE_LANE_ORANGE = "ProKeys.OrangeKey";
        public const string KEYS_STAR_POWER = "ProKeys.StarPower";
        public const string KEYS_TOUCH_EFFECTS = "ProKeys.TouchEffects";



    public const string DPAD_UP = "dpad/up";
        public const string DPAD_DOWN = "dpad/down";
        public const string DPAD_LEFT = "dpad/left";
        public const string DPAD_RIGHT = "dpad/right";

        public const string JOYSTICK_UP = "joystick/up";
        public const string JOYSTICK_DOWN = "joystick/down";
        public const string JOYSTICK_LEFT = "joystick/left";
        public const string JOYSTICK_RIGHT = "joystick/right";
    }
}
