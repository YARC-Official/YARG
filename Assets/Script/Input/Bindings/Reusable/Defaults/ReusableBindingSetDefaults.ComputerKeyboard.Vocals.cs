using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _computerKeyboardVocalDefaults = new()
        {
            {
                ControlStrings.VOCAL_HIT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ControlStrings.VOCAL_HIT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SPACE)
                )
            },
            {
                ControlStrings.VOCAL_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ControlStrings.VOCAL_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardVocalGameplay = MakeHardcodedBindingSet(
            "Default Vocals with Computer Keyboard",
            GameMode.Vocals,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardVocalDefaults
        );
    }
}
