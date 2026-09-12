using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Input.Bindings;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        private static Dictionary<string, ReusableControlBinding> _sixFretGuitarDefaults = new()
        {
            {
                ControlStrings.SIX_FRET_BLACK_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black1))
                )
            },
            {
                ControlStrings.SIX_FRET_BLACK_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black2))
                )
            },
            {
                ControlStrings.SIX_FRET_BLACK_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_BLACK_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black3))
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_1,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_1],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.white1))
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_2,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_2],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.white2))
                )
            },
            {
                ControlStrings.SIX_FRET_WHITE_3,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.SIX_FRET_WHITE_3],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.white3))
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_DOWN)
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_UP)
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.SIX_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(SixFretGuitar.tilt)) { PressPoint = 1f },
                        new(ControllerFamily.FiveFretGuitar, nameof(SixFretGuitar.selectButton)),
                    }
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.whammy))
                )
            },
        };

        private static Dictionary<string, ReusableControlBinding> _sixFretGuitarMenuDefaults = new()
        {
            {
                ControlStrings.MENU_START,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_START],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.startButton))
                )
            },
            {
                ControlStrings.MENU_SELECT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_SELECT],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.selectButton))
                )
            },

            {
                ControlStrings.MENU_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black1))
                )
            },
            {
                ControlStrings.MENU_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black2))
                )
            },
            {
                ControlStrings.MENU_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.black3))
                )
            },
            {
                ControlStrings.MENU_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.white1))
                )
            },
            {
                ControlStrings.MENU_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, nameof(SixFretGuitar.white2))
                )
            },

            {
                ControlStrings.MENU_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.DPAD_UP)
                )
            },
            {
                ControlStrings.MENU_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.SixFretGuitar, ControlStrings.DPAD_DOWN)
                )
            },

                        {
                ControlStrings.MENU_LEFT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_LEFT)
                )
            },
            {
                ControlStrings.MENU_RIGHT,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.MENU[ControlStrings.MENU_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, ControlStrings.DPAD_RIGHT)
                )
            },
        };

        public static ReusableBindingSet DefaultSixFretGuitar = MakeHardcodedBindingSet(
            "Default 6F Guitar Gameplay",
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
