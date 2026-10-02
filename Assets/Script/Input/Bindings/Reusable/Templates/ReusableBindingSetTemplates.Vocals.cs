using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core.Input;

namespace YARG.Input.Bindings
{

    public static partial class ReusableBindingSetTemplates
    {
        public static Dictionary<string, InputActionInfo> VOCALS = new()
        {
            { ControlStrings.VOCAL_HIT, new(ControlStrings.VOCAL_HIT,   BindingType.Button, (int) VocalsAction.Hit) },
            { ControlStrings.VOCAL_STAR_POWER, new(ControlStrings.VOCAL_STAR_POWER,   BindingType.Button, (int) VocalsAction.StarPower) },
        };
    }
}
