using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public const string FIVE_FRET_GREEN_FRET = "FiveFret.Green";

        public static Dictionary<string, InputActionInfo> FIVE_FRET_GUITAR = new()
        {
            { FIVE_FRET_GREEN_FRET, new(FIVE_FRET_GREEN_FRET, BindingType.Button, (int)GuitarAction.GreenFret) }
        };
    }
}
