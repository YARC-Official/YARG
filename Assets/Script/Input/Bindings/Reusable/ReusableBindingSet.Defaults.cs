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
        private static ReusableBindingSet MakeHardcodedBindingSet(
            string name,
            GameMode? mode,
            ControllerFamily controllerFamily,
            Dictionary<string, InputActionInfo> template,
            Dictionary<string, List<string>> defaults,
            bool isDefault = true
        )
        {
            var bindingSet = new ReusableBindingSet(name, mode, controllerFamily, isDefault);

            foreach (var (key, bindings) in defaults) {
                bindingSet.Bindings[key] = MakeHardcodedBinding(template[key], defaults[key]);
            }

            return bindingSet;
        }


        private static ReusableControlBinding MakeHardcodedBinding(InputActionInfo info, List<string> controls)
        {
            return info.Type switch
            {
                BindingType.Button or
                BindingType.IndividualButton => new ReusableButtonBinding(info, controls),
                BindingType.Axis => new ReusableAxisBinding(info, controls),
                BindingType.Integer => new ReusableIntegerBinding(info, controls)
            };
        }

        public static ReusableBindingSet DEFAULT_5F_GUITAR = MakeHardcodedBindingSet(
            "Default 5-Fret Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            ReusableBindingSetTemplates.FIVE_FRET_GUITAR,
            new()
            {
                { ControlStrings.FIVE_FRET_GREEN,         new() { nameof(FiveFretGuitar.greenFret) } },
                { ControlStrings.FIVE_FRET_RED,           new() { nameof(FiveFretGuitar.redFret) } },
                { ControlStrings.FIVE_FRET_YELLOW,        new() { nameof(FiveFretGuitar.yellowFret) } },
                { ControlStrings.FIVE_FRET_BLUE,          new() { nameof(FiveFretGuitar.blueFret) } },
                { ControlStrings.FIVE_FRET_ORANGE,        new() { nameof(FiveFretGuitar.orangeFret) } },

                { ControlStrings.FIVE_FRET_SOLO_GREEN,    new() { nameof(RockBandGuitar.soloGreen) } },
                { ControlStrings.FIVE_FRET_SOLO_RED,      new() { nameof(RockBandGuitar.soloRed) } },
                { ControlStrings.FIVE_FRET_SOLO_YELLOW,   new() { nameof(RockBandGuitar.soloYellow) } },
                { ControlStrings.FIVE_FRET_SOLO_BLUE,     new() { nameof(RockBandGuitar.soloBlue) } },
                { ControlStrings.FIVE_FRET_SOLO_ORANGE,   new() { nameof(RockBandGuitar.soloOrange) } },

                { ControlStrings.GUITAR_STRUM_UP,         new() { nameof(FiveFretGuitar.strumUp) } },
                { ControlStrings.GUITAR_STRUM_DOWN,       new() { nameof(FiveFretGuitar.strumDown) } },
                { ControlStrings.GUITAR_WHAMMY,           new() { nameof(FiveFretGuitar.whammy) } },

                { ControlStrings.GUITAR_STAR_POWER,       new() {
                    nameof(FiveFretGuitar.tilt),
                    nameof(FiveFretGuitar.selectButton) }
                },
            }
        );
    }
}
