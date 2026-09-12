using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FIVE_FRET_GUITAR = new()
        {
            { ControlStrings.FIVE_FRET_GREEN,          new(ControlStrings.FIVE_FRET_GREEN,        BindingType.Button,             (int) GuitarAction.GreenFret) },
            { ControlStrings.FIVE_FRET_RED,            new(ControlStrings.FIVE_FRET_RED,          BindingType.Button,             (int) GuitarAction.RedFret) },
            { ControlStrings.FIVE_FRET_YELLOW,         new(ControlStrings.FIVE_FRET_YELLOW,       BindingType.Button,             (int) GuitarAction.YellowFret) },
            { ControlStrings.FIVE_FRET_BLUE,           new(ControlStrings.FIVE_FRET_BLUE,         BindingType.Button,             (int) GuitarAction.BlueFret) },
            { ControlStrings.FIVE_FRET_ORANGE,         new(ControlStrings.FIVE_FRET_ORANGE,       BindingType.Button,             (int) GuitarAction.OrangeFret) },

            { ControlStrings.GUITAR_STRUM_UP,          new(ControlStrings.GUITAR_STRUM_UP,        BindingType.Button,             (int) GuitarAction.StrumUp) },
            { ControlStrings.GUITAR_STRUM_DOWN,        new(ControlStrings.GUITAR_STRUM_DOWN,      BindingType.Button,             (int) GuitarAction.StrumDown) },
            { ControlStrings.GUITAR_STAR_POWER,        new(ControlStrings.GUITAR_STAR_POWER,      BindingType.IndividualButton,   (int) GuitarAction.StarPower) },
            { ControlStrings.GUITAR_WHAMMY,            new(ControlStrings.GUITAR_WHAMMY,          BindingType.Axis,               (int) GuitarAction.Whammy) },

            { ControlStrings.FIVE_FRET_SOLO_GREEN,     new(ControlStrings.FIVE_FRET_SOLO_GREEN,   BindingType.Button,             (int) GuitarAction.SoloGreenFret) },
            { ControlStrings.FIVE_FRET_SOLO_RED,       new(ControlStrings.FIVE_FRET_SOLO_RED,     BindingType.Button,             (int) GuitarAction.SoloRedFret) },
            { ControlStrings.FIVE_FRET_SOLO_YELLOW,    new(ControlStrings.FIVE_FRET_SOLO_YELLOW,  BindingType.Button,             (int) GuitarAction.SoloYellowFret) },
            { ControlStrings.FIVE_FRET_SOLO_BLUE,      new(ControlStrings.FIVE_FRET_SOLO_BLUE,    BindingType.Button,             (int) GuitarAction.SoloBlueFret) },
            { ControlStrings.FIVE_FRET_SOLO_ORANGE,    new(ControlStrings.FIVE_FRET_SOLO_ORANGE,  BindingType.Button,             (int) GuitarAction.SoloOrangeFret) },

        };
    }
}
