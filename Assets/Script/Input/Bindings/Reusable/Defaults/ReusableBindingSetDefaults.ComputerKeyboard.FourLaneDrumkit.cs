using PlasticBand.Devices;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _defaultComputerKeyboardFiveLaneDrumsGameplay = new()
        {
            {
                ActionStrings.DRUMS_KICK,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.DRUMS_KICK],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SPACE),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ALT),
                    }
                )
            },

            {
                ActionStrings.FOUR_DRUMS_RED_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_RED_PAD],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_M),
                    }
                )
            },
            {
                ActionStrings.FOUR_DRUMS_YELLOW_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_PAD,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_PAD],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V)
                )
            },

            {
                ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_YELLOW_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_BLUE_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_BLUE_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },
            {
                ActionStrings.FOUR_DRUMS_GREEN_CYMBAL,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FOUR_LANE_DRUMKIT[ActionStrings.FOUR_DRUMS_GREEN_CYMBAL],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardFourLaneDrumsGameplay = MakeHardcodedBindingSet(
            "Default 4L Drums on Computer Keyboard",
            GameMode.FourLaneDrums,
            ControllerFamily.ComputerKeyboard,
            _defaultComputerKeyboardFiveLaneDrumsGameplay
        );
    }
}
