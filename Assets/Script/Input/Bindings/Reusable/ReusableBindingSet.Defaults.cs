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
        public static ReusableBindingSet DEFAULT_5F_GUITAR = new("Default 5-Fret Guitar", GameMode.FiveFretGuitar, ControllerFamily.FiveFretGuitar, true)
        {
            Bindings = new()
            {
                { "FiveFret.Green", MakeSimpleControlBinding(nameof(FiveFretGuitar.greenFret)) },
                { "FiveFret.Red", MakeSimpleControlBinding(nameof(FiveFretGuitar.redFret)) },
                { "FiveFret.Yellow", MakeSimpleControlBinding(nameof(FiveFretGuitar.yellowFret)) },
                { "FiveFret.Blue", MakeSimpleControlBinding(nameof(FiveFretGuitar.blueFret)) },
                { "FiveFret.Orange", MakeSimpleControlBinding(nameof(FiveFretGuitar.orangeFret)) },
            }
        };


        private static SerializedReusableControlBinding MakeSimpleControlBinding(string controlName)
        {
            return new SerializedReusableControlBinding()
            {
                Controls = new() { new(controlName) }
            };
        }
    }
}
