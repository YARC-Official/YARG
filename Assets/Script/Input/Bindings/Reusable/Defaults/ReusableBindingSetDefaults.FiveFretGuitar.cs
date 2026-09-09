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
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, "dpad/down")
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, "dpad/up")
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = 1f },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
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

            {
                ControlStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },

        };

        private static Dictionary<string, ReusableControlBinding> _riffmasterGuitarDefaults = new()
        {
            {
                ControlStrings.FIVE_FRET_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.greenFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.redFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.blueFret))
                )
            },
            {
                ControlStrings.FIVE_FRET_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret))
                )
            },

            {
                ControlStrings.GUITAR_STRUM_DOWN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_DOWN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumDown))
                )
            },
            {
                ControlStrings.GUITAR_STRUM_UP,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STRUM_UP],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.strumUp))
                )
            },

            {
                ControlStrings.GUITAR_STAR_POWER,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                    new List<ReusableSingleButtonBindingConfig>() {
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.tilt)) { PressPoint = 0.7f },
                        new(ControllerFamily.FiveFretGuitar, nameof(FiveFretGuitar.selectButton)),
                        new(ControllerFamily.FiveFretGuitar, nameof(GuitarHeroGuitar.spPedal)),
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

            {
                ControlStrings.FIVE_FRET_SOLO_GREEN,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_GREEN],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloGreen))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_RED,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_RED],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloRed))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_YELLOW,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_YELLOW],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloYellow))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_BLUE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_BLUE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloBlue))
                )
            },
            {
                ControlStrings.FIVE_FRET_SOLO_ORANGE,
                new ReusableButtonBinding(
                    ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_SOLO_ORANGE],
                    new ReusableSingleButtonBindingConfig(ControllerFamily.FiveFretGuitar, nameof(RockBandGuitar.soloOrange))
                )
            },

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
