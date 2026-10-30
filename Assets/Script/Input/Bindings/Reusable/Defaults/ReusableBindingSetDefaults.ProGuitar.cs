using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Helpers;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static readonly Dictionary<string, ReusableControlBinding> _defaultFiveFretGuitarOnProGuitar = new()
        {
            {
                ActionStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_GREEN_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_RED_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_YELLOW_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_BLUE_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_ORANGE_FRET)
                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.PRO_GUITAR_STRUM_6)
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.PRO_GUITAR_STRUM_1)
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_TILT) { PressPoint = DEFAULT_TILT_PRESS_POINT },
                        new(ControllerFamily.ProGuitar, ControlStrings.PLASTICBAND_SELECT),
                        new(ControllerFamily.ProGuitar, ControlStrings.PRO_GUITAR_DIGITAL_PEDAL),
                    }
                )
            },

            {
                ActionStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_GREEN_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_RED_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_BLUE_FRET)
                )
            },
            {
                ActionStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET)
                )
            },
        };

        private static readonly Dictionary<string, ReusableControlBinding> _proGuitarMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.PLASTICBAND_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.PLASTICBAND_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_GREEN_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_GREEN_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_BUTTON_SOUTH),
                    }
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_RED_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_RED_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_BUTTON_EAST),
                    }
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_YELLOW_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_YELLOW_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_BUTTON_NORTH),
                    }
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_BLUE_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_BLUE_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_BUTTON_WEST),
                    }
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new List<ReusableSingleButtonBindingConfig> () {
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_ORANGE_FRET),
                        new(ControllerFamily.ProGuitar, ControlStrings.GUITAR_5F_SOLO_ORANGE_FRET),
                    }
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_LEFT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RIGHT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.ProGuitar, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static readonly Guid GUID_5F_GUITAR_ON_PRO_GUITAR = new("00938199-778a-4ba6-ae41-6b49d1d935dd");
        public static readonly ReusableBindingSet DefaultFiveFretGuitarOnProGuitar = MakeHardcodedBindingSet(
            "Default 5F Guitar on Pro Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.ProGuitar,
            GUID_5F_GUITAR_ON_PRO_GUITAR,
            _defaultFiveFretGuitarOnProGuitar
        );

        private static Guid GUID_PRO_GUITAR_MENU = new("affb53a8-8f5c-4a82-bd58-2b9f6bde0347");
        public static readonly ReusableBindingSet DefaultProGuitarMenu = MakeHardcodedBindingSet(
            "Default Pro Guitar Menu",
            GameMode.Menu,
            ControllerFamily.ProGuitar,
            GUID_PRO_GUITAR_MENU,
            _proGuitarMenuDefaults
        );
    }
}