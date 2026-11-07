using System;
using System.Collections.Generic;
using YARG.Core;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public static partial class ReusableBindingSetDefaults
    {
        public static readonly Guid GUID_NONE = Guid.Empty;

        // Voodoo - this makes the static default initializers run early so we don't crunch
        // later in the menu. I have no idea why lmao
        public static void InitializeDefaults() {}

        private static ReusableBindingSet MakeHardcodedBindingSet(
            string name,
            GameMode mode,
            ControllerFamily family,
            Guid guid,
            Dictionary<string, ReusableControlBinding> bindings
        )
        {
            var bindingSet = new ReusableBindingSet(
                name,
                mode,
                family,
                guid,
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

            BindingsContainer.AddBindingSet(bindingSet);

            return bindingSet;
        }
    }
}
