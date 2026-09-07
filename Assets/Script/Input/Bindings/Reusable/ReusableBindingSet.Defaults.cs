using Melanchall.DryWetMidi.Common;
using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public partial class ReusableBindingSet
    {
        public static ReusableBindingSet DefaultFiveFretGuitar = new(
            "Default 5-Fret Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            true
        )
        {
            Bindings = new()
            {
                {
                    ControlStrings.FIVE_FRET_GREEN,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                        new ReusableSingleButtonBindingConfig() { ControlName = nameof(FiveFretGuitar.greenFret) }
                    )
                },
                {
                    ControlStrings.FIVE_FRET_RED,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                        new ReusableSingleButtonBindingConfig() { ControlName = nameof(FiveFretGuitar.redFret) }
                    )
                },

                {
                    ControlStrings.GUITAR_STAR_POWER,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                        new List<ReusableSingleButtonBindingConfig>() {
                            new() { ControlName = nameof(FiveFretGuitar.tilt), PressPoint = 1f },
                            new() { ControlName = nameof(FiveFretGuitar.selectButton) },
                            new() { ControlName = nameof(GuitarHeroGuitar.spPedal) },
                        }
                    )
                },
            }
        };

        public static ReusableBindingSet DefaultRiffmasterGuitar = new(
            "Default Riffmaster Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            true
        )
        {
            Bindings = new()
            {
                {
                    ControlStrings.FIVE_FRET_GREEN,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_GREEN],
                        new ReusableSingleButtonBindingConfig() { ControlName = nameof(FiveFretGuitar.greenFret) }
                    )
                },
                {
                    ControlStrings.FIVE_FRET_RED,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.FIVE_FRET_RED],
                        new ReusableSingleButtonBindingConfig() { ControlName = nameof(FiveFretGuitar.redFret) }
                    )
                },

                {
                    ControlStrings.GUITAR_STAR_POWER,
                    new ReusableButtonBinding(
                        ReusableBindingSetTemplates.FIVE_FRET_GUITAR[ControlStrings.GUITAR_STAR_POWER],
                        new List<ReusableSingleButtonBindingConfig>() {
                            new() { ControlName = nameof(FiveFretGuitar.tilt), PressPoint = 0.7f },
                            new() { ControlName = nameof(FiveFretGuitar.selectButton) },
                            new() { ControlName = nameof(GuitarHeroGuitar.spPedal) },
                        }
                    )
                },

            }
        };
    }
}
