using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> SIX_FRET_GUITAR = new()
        {
            { ControlStrings.SIX_FRET_BLACK_1,  new(ControlStrings.SIX_FRET_BLACK_1,    BindingType.Button, (int) GuitarAction.Black1Fret) },
            { ControlStrings.SIX_FRET_BLACK_2,  new(ControlStrings.SIX_FRET_BLACK_2,    BindingType.Button, (int) GuitarAction.Black2Fret) },
            { ControlStrings.SIX_FRET_BLACK_3,  new(ControlStrings.SIX_FRET_BLACK_3,    BindingType.Button, (int) GuitarAction.Black3Fret) },
            { ControlStrings.SIX_FRET_WHITE_1,  new(ControlStrings.SIX_FRET_WHITE_1,    BindingType.Button, (int) GuitarAction.White1Fret) },
            { ControlStrings.SIX_FRET_WHITE_2,  new(ControlStrings.SIX_FRET_WHITE_2,    BindingType.Button, (int) GuitarAction.White2Fret) },
            { ControlStrings.SIX_FRET_WHITE_3,  new(ControlStrings.SIX_FRET_WHITE_3,    BindingType.Button, (int) GuitarAction.White3Fret) },

            { ControlStrings.GUITAR_STRUM_UP,          new(ControlStrings.GUITAR_STRUM_UP,        BindingType.Button,             (int) GuitarAction.StrumUp) },
            { ControlStrings.GUITAR_STRUM_DOWN,        new(ControlStrings.GUITAR_STRUM_DOWN,      BindingType.Button,             (int) GuitarAction.StrumDown) },
            { ControlStrings.GUITAR_STAR_POWER,        new(ControlStrings.GUITAR_STAR_POWER,      BindingType.IndividualButton,   (int) GuitarAction.StarPower) },
            { ControlStrings.GUITAR_WHAMMY,            new(ControlStrings.GUITAR_WHAMMY,          BindingType.Axis,               (int) GuitarAction.Whammy) },

        };
    }
}
