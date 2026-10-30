using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine.InputSystem;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _computerKeyboardVocalDefaults = new()
        {
            {
                ActionStrings.VOCAL_HIT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_HIT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SPACE)
                )
            },
            {
                ActionStrings.VOCAL_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.VOCALS[ActionStrings.VOCAL_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
        };

        public static readonly Guid GUID_VOCALS_WITH_COMPUTER_KEYBOARD = new("137afaa3-c89c-4a89-8cde-515f7bf4e427");
        public static readonly ReusableBindingSet DefaultComputerKeyboardVocalGameplay = MakeHardcodedBindingSet(
            "Default Vocals with Computer Keyboard",
            GameMode.Vocals,
            ControllerFamily.ComputerKeyboard,
            GUID_VOCALS_WITH_COMPUTER_KEYBOARD,
            _computerKeyboardVocalDefaults
        );
    }
}
