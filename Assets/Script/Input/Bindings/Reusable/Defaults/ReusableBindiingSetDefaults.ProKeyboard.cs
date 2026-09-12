using PlasticBand.Devices;
using System.Collections.Generic;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _proKeyboardDefaults = new()
        {
            {
                ControlStrings.KEYS_PRO_KEY_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key1))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key2))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key3))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_4,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_4],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key4))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_5,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_5],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key5))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_6,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_6],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key6))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_7,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_7],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key7))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_8,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_8],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key8))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_9,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_9],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key9))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_10,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_10],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key10))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_11,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_11],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key11))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_12,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_12],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key12))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_13,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_13],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key13))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_14,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_14],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key14))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_15,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_15],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key15))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_16,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_16],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key16))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_17,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_17],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key17))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_18,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_18],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key18))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_19,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_19],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key19))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_20,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_20],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key20))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_21,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_21],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key21))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_22,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_22],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key22))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_23,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_23],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key23))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_24,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_24],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key24))
                )
            },
            {
                ControlStrings.KEYS_PRO_KEY_25,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_PRO_KEY_25],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.key25))
                )
            },

            {
                ControlStrings.KEYS_FIVE_LANE_OPEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_OPEN],
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
                ControlStrings.KEYS_FIVE_LANE_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_GREEN],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key1)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key13)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key25))
                    }
                    
                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_RED],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key3)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key15))
                    }

                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_YELLOW],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key5)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key17))
                    }

                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_BLUE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key6)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key18))
                    }

                )
            },
            {
                ControlStrings.KEYS_FIVE_LANE_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_FIVE_LANE_ORANGE],
                    new List<ReusableSingleButtonBindingConfig>()
                    {
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key8)),
                        new (ControllerFamily.ProKeyboard, nameof(ProKeyboard.key20))
                    }

                )
            },

            {
                ControlStrings.KEYS_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_STAR_POWER],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.overdrive))
                )
            },
            {
                ControlStrings.KEYS_TOUCH_EFFECTS,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.KEYS[ControlStrings.KEYS_TOUCH_EFFECTS],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.touchStrip))
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _proKeyboardDefaultMenu = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonSouth))
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonEast))
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonNorth))
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, nameof(ProKeyboard.buttonWest))
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.DPAD_UP)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.DPAD_DOWN)
                )
            },
            {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.DPAD_LEFT)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProKeyboard, ControlStrings.DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultProKeyboard = MakeHardcodedBindingSet(
            "Default Pro Keyboard",
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