using PlasticBand.Devices;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _proKeyboardDefaults = new()
        {
            {
                ActionStrings.KEYS_PRO_KEY_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key1))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key2))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key3))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_4,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_4],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key4))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_5,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_5],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key5))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_6,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_6],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key6))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_7,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_7],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key7))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_8,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_8],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key8))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_9,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_9],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key9))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_10,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_10],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key10))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_11,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_11],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key11))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_12,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_12],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key12))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_13,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_13],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key13))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_14,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_14],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key14))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_15,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_15],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key15))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_16,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_16],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key16))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_17,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_17],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key17))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_18,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_18],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key18))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_19,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_19],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key19))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_20,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_20],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key20))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_21,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_21],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key21))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_22,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_22],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key22))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_23,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_23],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key23))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_24,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_24],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key24))
                )
            },
            {
                ActionStrings.KEYS_PRO_KEY_25,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_PRO_KEY_25],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key25))
                )
            },

            {
                ActionStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_OPEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key10)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key12)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key22)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key24)),
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_GREEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key1)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key13)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key25))
                    }
                    
                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_RED],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key3)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key15))
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key5)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key17))
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_BLUE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key6)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key18))
                    }

                )
            },
            {
                ActionStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_FIVE_LANE_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key8)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key20))
                    }

                )
            },

            {
                ActionStrings.KEYS_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.overdrive))
                )
            },
            {
                ActionStrings.KEYS_TOUCH_EFFECTS,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.KEYS[ActionStrings.KEYS_TOUCH_EFFECTS],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.touchStrip))
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _proKeyboardDefaultMenu = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.startButton))
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.selectButton))
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonSouth))
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonEast))
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonNorth))
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonWest))
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultProKeyboard = MakeHardcodedBindingSet(
            "Default Pro Keys",
            GameMode.ProKeys,
            ControllerFamily.ProKeyboard,
            _proKeyboardDefaults
        );

        public static ReusableBindingSet DefaultProKeyboardMenu = MakeHardcodedBindingSet(
            "Default Pro Keyboard Menu",
            GameMode.Menu,
            ControllerFamily.ProKeyboard,
            _proKeyboardDefaultMenu
        );
    }
}