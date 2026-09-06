using Melanchall.DryWetMidi.Common;
using PlasticBand.Devices;
using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input
{
    public partial class ReusableBindingSet
    {
        public static ReusableBindingSet DEFAULT_5F_GUITAR = new(
            "Default 5-Fret Guitar",
            GameMode.FiveFretGuitar,
            ControllerFamily.FiveFretGuitar,
            new()
            {
                { ReusableBindingSetTemplates.FIVE_FRET_GREEN,     MakeSimpleControlBinding(GameMode.FiveFretGuitar, ReusableBindingSetTemplates.FIVE_FRET_GREEN,   nameof(FiveFretGuitar.greenFret)) },
                { ReusableBindingSetTemplates.FIVE_FRET_RED,       MakeSimpleControlBinding(GameMode.FiveFretGuitar, ReusableBindingSetTemplates.FIVE_FRET_RED,     nameof(FiveFretGuitar.redFret)) },
                { ReusableBindingSetTemplates.FIVE_FRET_YELLOW,    MakeSimpleControlBinding(GameMode.FiveFretGuitar, ReusableBindingSetTemplates.FIVE_FRET_YELLOW,  nameof(FiveFretGuitar.yellowFret)) },
                { ReusableBindingSetTemplates.FIVE_FRET_BLUE,      MakeSimpleControlBinding(GameMode.FiveFretGuitar, ReusableBindingSetTemplates.FIVE_FRET_BLUE,    nameof(FiveFretGuitar.blueFret)) },
                { ReusableBindingSetTemplates.FIVE_FRET_ORANGE,    MakeSimpleControlBinding(GameMode.FiveFretGuitar, ReusableBindingSetTemplates.FIVE_FRET_ORANGE,  nameof(FiveFretGuitar.orangeFret)) },
            },
            true
        );


        private static ReusableControlBinding MakeSimpleControlBinding(GameMode mode, string bindingLocalizationKey, string singleBindingName)
        {
            var template = ReusableBindingSetTemplates.GetTemplateForGameMode(mode);

            var bindingType = template[singleBindingName];

            return bindingType switch
            {
                BindingType.Button => new ReusableButtonBinding(bindingLocalizationKey, new List<ReusableSingleButtonBinding>() { new(singleBindingName) }),
                BindingType.Axis => new ReusableAxisBinding  (bindingLocalizationKey, new List<ReusableSingleAxisBinding>() { new(singleBindingName) }),
                BindingType.Integer => new ReusableIntegerBinding(bindingLocalizationKey, new List<ReusableSingleIntegerBinding>() { new(singleBindingName) }),
                _ => throw new ArgumentOutOfRangeException("Unexpected binding type!")
            };
        }
    }
}
