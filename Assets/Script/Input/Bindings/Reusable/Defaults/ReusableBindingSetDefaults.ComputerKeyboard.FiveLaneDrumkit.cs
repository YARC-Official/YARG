using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _computerKeyboardFiveLaneDrumsGameplayDefaults = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SPACE),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ALT),
                    }
                )
            },

            {
                ActionStrings.FIVE_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_RED_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_ORANGE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V)
                )
            },

            {
                ActionStrings.FIVE_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_LANE_DRUMKIT[ActionStrings.FIVE_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_B)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardFiveLaneDrumsGameplay = MakeHardcodedBindingSet(
            "Default 5L Drums on Computer Keyboard",
            GameMode.FiveLaneDrums,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardFiveLaneDrumsGameplayDefaults
        );
    }
}
