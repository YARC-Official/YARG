using PlasticBand.Devices;
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
        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                    }
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                    }
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_A)
                    }
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_ARROW),
                        new (ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                    }
                )
            },

            {
                ActionStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ActionStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT_ARTIST],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F1)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuArrowKeysOnly = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_UP_ARROW)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DOWN_ARROW)
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_ARROW)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_RIGHT_ARROW)
                )
            },

            {
                ActionStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ActionStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT_ARTIST],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F1)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _computerKeyboardMenuWASDOnly = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_ENTER)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_BACKSPACE)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_1)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_2)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_3)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_4)
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_DIGIT_5)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_A)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },

            {
                ActionStrings.MENU_SEARCH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SEARCH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_TAB)
                )
            },

            {
                ActionStrings.MENU_SELECT_ARTIST,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT_ARTIST],
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
