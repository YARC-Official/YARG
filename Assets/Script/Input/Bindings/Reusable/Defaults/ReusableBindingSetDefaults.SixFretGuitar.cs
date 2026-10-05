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
        private static Dictionary<string, ReusableControlBinding> _sixFretGuitarDefaults = new()
        {
            {
                ActionStrings.SIX_FRET_BLACK_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_1)
                )
            },
            {
                ActionStrings.SIX_FRET_BLACK_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_2)
                )
            },
            {
                ActionStrings.SIX_FRET_BLACK_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_BLACK_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_3)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_WHITE_1)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_WHITE_2)
                )
            },
            {
                ActionStrings.SIX_FRET_WHITE_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.SIX_FRET_WHITE_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_WHITE_3)
                )
            },

            {
                ActionStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },
            {
                ActionStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },

            {
                ActionStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ActionStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(SixFretGuitar.tilt)) { PressPoint = 1f },
                        new(ControllerFamily.FiveFretGuitar, nameof(SixFretGuitar.selectButton)),
                    }
                )
            },

            {
                ActionStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ActionStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_WHAMMY)
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _sixFretGuitarMenuDefaults = new()
        {
            {
                ActionStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_START)
                )
            },
            {
                ActionStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_SELECT)
                )
            },

            {
                ActionStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_1)
                )
            },
            {
                ActionStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_2)
                )
            },
            {
                ActionStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_BLACK_3)
                )
            },
            {
                ActionStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_WHITE_1)
                )
            },
            {
                ActionStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GUITAR_6F_WHITE_2)
                )
            },

            {
                ActionStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_UP)
                )
            },
            {
                ActionStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_DOWN)
                )
            },

            {
                ActionStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_LEFT)
                )
            },
            {
                ActionStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ActionStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.GAMEPAD_DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultSixFretGuitar = MakeHardcodedBindingSet(
            "Default 6F Guitar",
            GameMode.SixFretGuitar,
            ControllerFamily.SixFretGuitar,
            _sixFretGuitarDefaults
        );

        public static ReusableBindingSet DefaultSixFretGuitarMenu = MakeHardcodedBindingSet(
            "Default 6F Guitar Menu",
            GameMode.Menu,
            ControllerFamily.SixFretGuitar,
            _sixFretGuitarMenuDefaults
        );
    }
}
