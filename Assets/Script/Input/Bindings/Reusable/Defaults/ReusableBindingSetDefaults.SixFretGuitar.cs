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
        public static ReusableBindingSet DefaultSixFretGuitar = MakeHardcodedBindingSet(
            "Default",
            GameMode.SixFretGuitar,
            ControllerFamily.SixFretGuitar,
            _sixFretGuitarDefaults
        );
    }
}
