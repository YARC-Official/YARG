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
    public static partial class ReusableBindingSetDefaults
    {
        private static ReusableBindingSet MakeHardcodedBindingSet(
            string name,
            GameMode mode,
            ControllerFamily family,
            Dictionary<string, ReusableControlBinding> bindings
        )
        {
            var bindingSet = new ReusableBindingSet(
                name,
                mode,
                family,
                true
            );

            var template = ReusableBindingSetTemplates.GetTemplate(mode);

            foreach (var (key, info) in template)
            {
                if (bindings.ContainsKey(key))
                {
                    bindingSet.AddBinding(key, bindings[key]);
                }
                else
                {
                    bindingSet.AddBinding(key, info.Type switch
                    {
                        BindingType.Button or BindingType.Impulse => new ReusableButtonBinding(info),
                        BindingType.Axis => new ReusableAxisBinding(info),
                        BindingType.Integer => new ReusableIntegerBinding(info),
                        _ => throw new ArgumentOutOfRangeException("Unreachable")
                    });
                }
            }

            return bindingSet;
        }
    }
}
