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
                { "FiveFret.Green",     MakeSimpleControlBinding(GameMode.FiveFretGuitar, nameof(FiveFretGuitar.greenFret)) },
                { "FiveFret.Red",       MakeSimpleControlBinding(GameMode.FiveFretGuitar, nameof(FiveFretGuitar.redFret)) },
                { "FiveFret.Yellow",    MakeSimpleControlBinding(GameMode.FiveFretGuitar, nameof(FiveFretGuitar.yellowFret)) },
                { "FiveFret.Blue",      MakeSimpleControlBinding(GameMode.FiveFretGuitar, nameof(FiveFretGuitar.blueFret)) },
                { "FiveFret.Orange",    MakeSimpleControlBinding(GameMode.FiveFretGuitar, nameof(FiveFretGuitar.orangeFret)) },
            },
            true
        );


        private static ReusableControlBinding MakeSimpleControlBinding(GameMode mode, string controlName)
        {
            var template = ReusableBindingSetTemplates.GetTemplateForGameMode(mode);

            var bindingType = template[controlName];

            return new ReusableControlBinding(bindingType)
            {
                Controls = new() { new(controlName) }
            };
        }
    }
}
