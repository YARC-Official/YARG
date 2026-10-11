using System.Collections.Generic;
using YARG.Helpers;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> FIVE_FRET_GUITAR = new()
        {
            { ActionStrings.FIVE_FRET_GREEN,          new(ActionStrings.FIVE_FRET_GREEN,        BindingType.Button,             (int) GuitarAction.GreenFret) },
            { ActionStrings.FIVE_FRET_RED,            new(ActionStrings.FIVE_FRET_RED,          BindingType.Button,             (int) GuitarAction.RedFret) },
            { ActionStrings.FIVE_FRET_YELLOW,         new(ActionStrings.FIVE_FRET_YELLOW,       BindingType.Button,             (int) GuitarAction.YellowFret) },
            { ActionStrings.FIVE_FRET_BLUE,           new(ActionStrings.FIVE_FRET_BLUE,         BindingType.Button,             (int) GuitarAction.BlueFret) },
            { ActionStrings.FIVE_FRET_ORANGE,         new(ActionStrings.FIVE_FRET_ORANGE,       BindingType.Button,             (int) GuitarAction.OrangeFret) },

            { ActionStrings.GUITAR_STRUM_UP,          new(ActionStrings.GUITAR_STRUM_UP,        ActionStrings.GUITAR_STRUM_DOWN,BindingType.Button,             (int) GuitarAction.StrumUp) },
            { ActionStrings.GUITAR_STRUM_DOWN,        new(ActionStrings.GUITAR_STRUM_DOWN,      ActionStrings.GUITAR_STRUM_UP,  BindingType.Button,             (int) GuitarAction.StrumDown) },
            { ActionStrings.GUITAR_STAR_POWER,        new(ActionStrings.GUITAR_STAR_POWER,      BindingType.Button,             (int) GuitarAction.StarPower) },
            { ActionStrings.GUITAR_WHAMMY,            new(ActionStrings.GUITAR_WHAMMY,          BindingType.Axis,               (int) GuitarAction.Whammy) },

            { ActionStrings.FIVE_FRET_SOLO_GREEN,     new(ActionStrings.FIVE_FRET_SOLO_GREEN,   BindingType.Button,             (int) GuitarAction.SoloGreenFret,     false) },
            { ActionStrings.FIVE_FRET_SOLO_RED,       new(ActionStrings.FIVE_FRET_SOLO_RED,     BindingType.Button,             (int) GuitarAction.SoloRedFret,       false) },
            { ActionStrings.FIVE_FRET_SOLO_YELLOW,    new(ActionStrings.FIVE_FRET_SOLO_YELLOW,  BindingType.Button,             (int) GuitarAction.SoloYellowFret,    false) },
            { ActionStrings.FIVE_FRET_SOLO_BLUE,      new(ActionStrings.FIVE_FRET_SOLO_BLUE,    BindingType.Button,             (int) GuitarAction.SoloBlueFret,      false) },
            { ActionStrings.FIVE_FRET_SOLO_ORANGE,    new(ActionStrings.FIVE_FRET_SOLO_ORANGE,  BindingType.Button,             (int) GuitarAction.SoloOrangeFret,    false) },
        };
    }
}
