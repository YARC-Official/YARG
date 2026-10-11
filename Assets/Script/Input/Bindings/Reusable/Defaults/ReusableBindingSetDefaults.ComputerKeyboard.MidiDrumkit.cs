using System.Collections.Generic;
using YARG.Assets.Script.Helpers;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _defaultComputerKeyboardDrumsGameplayDefaults = new()
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
                ActionStrings.ELITE_DRUMS_STOMP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_STOMP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_CTRL)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_SPLASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_SPLASH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_LEFT_SHIFT)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_SNARE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_SNARE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_CLOSED_HI_HAT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },
            
            {
                ActionStrings.ELITE_DRUMS_OPEN_HI_HAT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_OPEN_HI_HAT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_W)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_LEFT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_LEFT_CRASH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_TOM_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_TOM_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_TOM_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_TOM_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_TOM_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_TOM_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_RIDE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_RIDE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_RIGHT_CRASH,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_RIGHT_CRASH],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_G)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_4L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_YTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_YTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_BTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_BTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_GTOM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_GTOM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_4L_YCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_YCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_S)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_BCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_BCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_D)
                )
            },
            {
                ActionStrings.ELITE_DRUMS_4L_GCYM,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_4L_GCYM],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_F)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_Z)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_X)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_C)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_V)
                )
            },

            {
                ActionStrings.ELITE_DRUMS_5L_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.ELITE_DRUMS[ActionStrings.ELITE_DRUMS_5L_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ComputerKeyboard, ControlStrings.COMPUTER_KEYBOARD_B)
                )
            },
        };

        public static ReusableBindingSet DefaultComputerKeyboardDrumsGameplay = MakeHardcodedBindingSet(
            "Default Drums on Computer Keyboard",
            GameMode.EliteDrums,
            ControllerFamily.ComputerKeyboard,
            _defaultComputerKeyboardDrumsGameplayDefaults
        );
    }
}
