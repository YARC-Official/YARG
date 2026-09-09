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
        private static Dictionary<string, ReusableControlBinding> _fiveFretGuitarDefaults = new()
        {
            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(nameof(FiveFretGuitar.greenFret), LayoutStrings.FIVE_FRET_GUITAR)
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(nameof(FiveFretGuitar.redFret), LayoutStrings.FIVE_FRET_GUITAR)
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(nameof(FiveFretGuitar.tilt),         LayoutStrings.FIVE_FRET_GUITAR) { PressPoint = 1f },
                        new(nameof(FiveFretGuitar.selectButton), LayoutStrings.FIVE_FRET_GUITAR),
                        new(nameof(GuitarHeroGuitar.spPedal),    LayoutStrings.GUITAR_HERO_GUITAR),
                    }
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(nameof(FiveFretGuitar.whammy), LayoutStrings.FIVE_FRET_GUITAR)
                )
            }
        };

        private static Dictionary<string, ReusableControlBinding> _riffmasterGuitarDefaults = new()
        {
            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(nameof(FiveFretGuitar.greenFret), LayoutStrings.FIVE_FRET_GUITAR)
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(nameof(FiveFretGuitar.redFret), LayoutStrings.FIVE_FRET_GUITAR)
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(nameof(FiveFretGuitar.tilt),         LayoutStrings.FIVE_FRET_GUITAR) { PressPoint = 0.7f },
                        new(nameof(FiveFretGuitar.selectButton), LayoutStrings.FIVE_FRET_GUITAR),
                        new(nameof(GuitarHeroGuitar.spPedal),    LayoutStrings.GUITAR_HERO_GUITAR),
                    }
                )
            },

            {
                ControlStrings.GUITAR_WHAMMY,
                new ReusableAxisBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_WHAMMY],
                    new ReusableSingleAxisBindingConfig(nameof(FiveFretGuitar.whammy), LayoutStrings.FIVE_FRET_GUITAR)
                )
            }
        };

        public static ReusableBindingSet DefaultFiveFretGuitar = MakeHardcodedBindingSet(
            "Default 5-Fret Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            _fiveFretGuitarDefaults,
            ReusableBindingSetTemplates.FIVE_FRET_GUITAR
        );

        public static ReusableBindingSet DefaultRiffmasterGuitar = MakeHardcodedBindingSet(
            "Default Riffmaster Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            _riffmasterGuitarDefaults,
            ReusableBindingSetTemplates.FIVE_FRET_GUITAR
        );
    }
}
