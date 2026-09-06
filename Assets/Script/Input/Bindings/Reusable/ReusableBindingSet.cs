using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;
using static YARG.Input.Serialization.SerializedBindingsV4;

namespace YARG.Input
{
    public partial class ReusableBindingSet
    {
        public Guid Guid { get; private set; }
        public GameMode? Mode { get; } // null means menu bindings
        public ControllerFamily ControllerFamily { get; private set; }
        public string Name { get; private set; }
        public bool IsDefault { get; private set; }

        // Key is binding name, like "FiveFret.Green" or "FourDrums.RedPad"
        // These names come from BindingCollection.Templates.cs; they are YARG's, not PlasticBand's
        public Dictionary<string, ReusableControlBinding> Bindings = new();

        public ReusableBindingSet(
            string name,
            GameMode? mode,
            ControllerFamily controllerFamily,
            Dictionary<string, ReusableControlBinding> bindings,
            bool isDefault = false
        ) {
            Name = name;
            Mode = mode;
            ControllerFamily = controllerFamily;
            Guid = isDefault ? Guid.Empty : Guid.NewGuid();
            IsDefault = isDefault;
            Bindings = bindings;

        }

        public ReusableBindingSet(SerializedReusableBindingSet serialized)
        {
            Name = serialized.Name;
            Guid = serialized.Guid;
            Mode = serialized.GameMode;
            ControllerFamily = LayoutHelper.LayoutStringToControllerFamily(serialized.BaseLayout);

            var template = ReusableBindingSetTemplates.GetTemplateForGameMode(Mode);

            foreach (var (name, serializedControlBinding) in serialized.Bindings)
            {
                var type = template[name];

                Bindings[name] = type switch {
                    BindingType.Button => new ReusableButtonBinding(serializedControlBinding),
                    BindingType.Axis => new ReusableAxisBinding(serializedControlBinding),
                    BindingType.Integer => new ReusableIntegerBinding(serializedControlBinding),
                    _ => throw new ArgumentOutOfRangeException("Unexpected binding type"),
                };
            }
            

            AddRemainingBindings(template);
        }

        private void AddRemainingBindings(Dictionary<string, BindingType> template)
        {
            foreach (var (name, type) in template)
            {
                if (!Bindings.ContainsKey(name))
                {
                    Bindings[name] = type switch
                    {
                        BindingType.Button => new ReusableButtonBinding(),
                        BindingType.Axis => new ReusableAxisBinding(),
                        BindingType.Integer => new ReusableIntegerBinding(),
                        _ => throw new ArgumentOutOfRangeException("Unexpected binding type"),
                    };
                }
            }
        }

#nullable enable
        public SerializedReusableBindingSet? Serialize()
        {
            if (Bindings.Count < 1)
            {
                return null;
            }

            var serializedBindings = new Dictionary<string, SerializedReusableControlBinding>();
            foreach (var (name, binding) in Bindings)
            {
                serializedBindings[name] = binding.Serialize();
            }

            return new SerializedReusableBindingSet(Name, LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily)) {
                Guid = Guid,
                GameMode = Mode,
                Bindings = serializedBindings,
                BaseLayout = LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily),
            };
        }
    }
}
