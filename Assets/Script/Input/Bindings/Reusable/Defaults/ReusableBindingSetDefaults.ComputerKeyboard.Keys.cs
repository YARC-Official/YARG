using PlasticBand.Devices;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _computerKeyboardKeysGameplayDefaults = new()
        {
            {
                ControlStrings.KEYS_PRO_KEY_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_4,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_4],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_5,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_5],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_6,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_6],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Q),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_7,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_7],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_G),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_8,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_8],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_B),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_9,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_9],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_H),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_10,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_10],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_N),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_E),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_11,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_11],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_J),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_12,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_12],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_M),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_R),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_13,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_13],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_COMMA),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_T),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_14,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_14],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_L),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_6),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_15,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_15],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_PERIOD),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Y),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_16,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_16],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SEMICOLON),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_7),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_17,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_17],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SLASH),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_U),
                    }
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_18,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_18],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_I)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_19,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_19],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_9)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_20,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_20],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_O)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_21,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_21],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_0)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_22,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_22],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_P)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_23,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_23],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_MINUS)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_24,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_24],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_BRACKET)
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_25,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_25],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_BRACKET)
                )
            },

            {
                ControlStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_OPEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACK_QUOTE), // The left-of-green option for Dedicated Open Lane users
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SPACE), // By analogy to the guitar-on-keyboard bindings
                    }

                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ControlStrings.KEYS_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },
            {
                ControlStrings.KEYS_TOUCH_EFFECTS,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_TOUCH_EFFECTS],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_QUOTE)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardKeysGameplay = MakeHardcodedBindingSet(
            "Default Computer Keyboard Keys Gameplay",
            GameMode.ProKeys,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardKeysGameplayDefaults
        );
    }
}
