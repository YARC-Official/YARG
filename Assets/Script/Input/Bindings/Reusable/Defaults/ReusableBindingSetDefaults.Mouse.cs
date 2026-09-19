using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _mouseVocalDefaults = new()
        {
            {
                ControlStrings.VOCAL_HIT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ControlStrings.VOCAL_HIT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Mouse, ControlStrings.MOUSE_LEFT_CLICK)
                )
            },
            {
                ControlStrings.VOCAL_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ControlStrings.VOCAL_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Mouse, ControlStrings.MOUSE_RIGHT_CLICK)
                )
            },
        };

        public static ReusableBindingSet DefaultMouseVocalGameplay = MakeHardcodedBindingSet(
            "Default Vocals with Mouse",
            GameMode.Vocals,
            ControllerFamily.Mouse,
            _mouseVocalDefaults
        );
    }
}
