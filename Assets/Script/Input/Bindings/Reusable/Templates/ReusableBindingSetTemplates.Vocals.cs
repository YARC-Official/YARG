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
        public static Dictionary<string, InputActionInfo> VOCALS = new()
        {
            { ActionStrings.VOCAL_HIT, new(ActionStrings.VOCAL_HIT,   BindingType.Button, (int) VocalsAction.Hit) },
            { ActionStrings.VOCAL_STAR_POWER, new(ActionStrings.VOCAL_STAR_POWER,   BindingType.Button, (int) VocalsAction.StarPower) },
        };
    }
}
