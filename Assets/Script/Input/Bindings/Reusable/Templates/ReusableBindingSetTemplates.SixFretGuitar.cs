using System;
using System.Collections.Generic;
using System.Text;
using YARG.Assets.Script.Helpers;
using YARG.Core.Input;
using YARG.Helpers;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> SIX_FRET_GUITAR = new()
        {
            { ActionStrings.SIX_FRET_BLACK_1,  new(ActionStrings.SIX_FRET_BLACK_1,    BindingType.Button, (int) GuitarAction.Black1Fret) },
            { ActionStrings.SIX_FRET_BLACK_2,  new(ActionStrings.SIX_FRET_BLACK_2,    BindingType.Button, (int) GuitarAction.Black2Fret) },
            { ActionStrings.SIX_FRET_BLACK_3,  new(ActionStrings.SIX_FRET_BLACK_3,    BindingType.Button, (int) GuitarAction.Black3Fret) },
            { ActionStrings.SIX_FRET_WHITE_1,  new(ActionStrings.SIX_FRET_WHITE_1,    BindingType.Button, (int) GuitarAction.White1Fret) },
            { ActionStrings.SIX_FRET_WHITE_2,  new(ActionStrings.SIX_FRET_WHITE_2,    BindingType.Button, (int) GuitarAction.White2Fret) },
            { ActionStrings.SIX_FRET_WHITE_3,  new(ActionStrings.SIX_FRET_WHITE_3,    BindingType.Button, (int) GuitarAction.White3Fret) },

            { ActionStrings.GUITAR_STRUM_UP,          new(ActionStrings.GUITAR_STRUM_UP,        ActionStrings.GUITAR_STRUM_DOWN,BindingType.Button,             (int) GuitarAction.StrumUp) },
            { ActionStrings.GUITAR_STRUM_DOWN,        new(ActionStrings.GUITAR_STRUM_DOWN,      ActionStrings.GUITAR_STRUM_UP,  BindingType.Button,             (int) GuitarAction.StrumDown) },
            { ActionStrings.GUITAR_STAR_POWER,        new(ActionStrings.GUITAR_STAR_POWER,      BindingType.Button,             (int) GuitarAction.StarPower) },
            { ActionStrings.GUITAR_WHAMMY,            new(ActionStrings.GUITAR_WHAMMY,          BindingType.Axis,               (int) GuitarAction.Whammy) },

        };
    }
}
