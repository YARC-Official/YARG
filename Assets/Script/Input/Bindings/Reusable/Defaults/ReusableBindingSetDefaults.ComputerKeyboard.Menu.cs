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
        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                    }
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                    }
                )
            },

                        {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_A)
                    }
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                    }
                )
            },

            {
                ControlStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ControlStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT_ARTIST],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F1)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuArrowKeysOnly = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW)
                )
            },

                        {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ARROW)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_ARROW)
                )
            },

            {
                ControlStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ControlStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT_ARTIST],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F1)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuWASDOnly = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },

                        {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_A)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },

            {
                ControlStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ControlStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT_ARTIST],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F1)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardMenu = MakeHardcodedBindingSet(
            "Default Computer Keyboard Menu",
            GameMode.Menu,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardMenuDefaults
        );

        public static ReusableBindingSet ComputerKeyboardMenuArrowKeysOnly = MakeHardcodedBindingSet(
            "Arrow Keys Only",
            GameMode.Menu,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardMenuArrowKeysOnly
        );

        public static ReusableBindingSet ComputerKeyboardMenuWASDOnly = MakeHardcodedBindingSet(
            "WASD Only",
            GameMode.Menu,
            ControllerFamily.ComputerKeyboard,
            _computerKeyboardMenuWASDOnly
        );
    }
}
