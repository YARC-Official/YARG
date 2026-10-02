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
        private static Dictionary<string, ReusableControlBinding> _defaultComputerKeyboardSixFretGuitarGameplay = new()
        {
            {
                ControlStrings.SIX_FRET_BLACK_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.SIX_FRET_BLACK_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.SIX_FRET_BLACK_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Q)
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_E)
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_SHIFT),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW),
                    }
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER),
                        new(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW),
                    }
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
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
        };

        public static ReusableBindingSet DefaultComputerKeyboardSixFretGuitarGameplay = MakeHardcodedBindingSet(
            "Default 6F Guitar on Computer Keyboard",
            GameMode.SixFretGuitar,
            ControllerFamily.ComputerKeyboard,
            _defaultComputerKeyboardSixFretGuitarGameplay
        );
    }
}
