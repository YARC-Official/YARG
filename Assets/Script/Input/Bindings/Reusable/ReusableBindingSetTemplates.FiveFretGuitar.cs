using System.Collections.Generic;

namespace YARG.Input
{
    public static partial class ReusableBindingSetTemplates
    {
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
        public const string GUITAR_STRUM_UP = "Guitar.StrumUp";
        public const string GUITAR_STRUM_DOWN = "Guitar.StrumDown";
        public const string GUITAR_STAR_POWER = "Guitar.StarPower";
        public const string GUITAR_WHAMMY = "Guitar.Whammy";

        private static Dictionary<string, BindingType> FiveFretGuitar = new()
        {
            {FIVE_FRET_GREEN,       BindingType.Button},
            {FIVE_FRET_RED,         BindingType.Button},
            {FIVE_FRET_YELLOW,      BindingType.Button},
            {FIVE_FRET_BLUE,        BindingType.Button},
            {FIVE_FRET_ORANGE,      BindingType.Button},
            {FIVE_FRET_SOLO_GREEN,  BindingType.Button},
            {FIVE_FRET_SOLO_RED,    BindingType.Button},
            {FIVE_FRET_SOLO_YELLOW, BindingType.Button},
            {FIVE_FRET_SOLO_BLUE,   BindingType.Button},
            {FIVE_FRET_SOLO_ORANGE, BindingType.Button},
            {GUITAR_STRUM_UP,       BindingType.Button},
            {GUITAR_STRUM_DOWN,     BindingType.Button},
            {GUITAR_STAR_POWER,     BindingType.Button},
            {GUITAR_WHAMMY,         BindingType.Axis },
        };


    }

}
