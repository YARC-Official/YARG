using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _defaultComputerKeyboardFiveFretGuitarGameplay = new()
        {

            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ControlStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_SHIFT),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW),
                    }
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW),
                    }
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_SEMICOLON)
                )
            },

            {
                ControlStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Q)
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_E)
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_R)
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_T)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardFiveFretGuitarGameplay = MakeHardcodedBindingSet(
            "Default 5F Guitar on Computer Keyboard",
            GameMode.FiveFretGuitar,
            ControllerFamily.ComputerKeyboard,
            _defaultComputerKeyboardFiveFretGuitarGameplay
        );
    }
}
