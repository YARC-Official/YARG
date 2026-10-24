using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _mouseVocalDefaults = new()
        {
            {
                ActionStrings.VOCAL_HIT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_HIT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Mouse, ControlStrings.MOUSE_LEFT_CLICK)
                )
            },
            {
                ActionStrings.VOCAL_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.Mouse, ControlStrings.MOUSE_RIGHT_CLICK)
                )
            },
        };

        public static readonly Guid GUID_VOCALS_WITH_MOUSE = new("23ca5868-6670-460b-98af-49b00ef7e942");
        public static ReusableBindingSet DefaultMouseVocalGameplay = MakeHardcodedBindingSet(
            "Default Vocals with Mouse",
            GameMode.Vocals,
            ControllerFamily.Mouse,
            GUID_VOCALS_WITH_MOUSE,
            _mouseVocalDefaults
        );
    }
}
