using System;
using System.Collections.Generic;
using System.Text;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Serialization;
using YARG.Menu.ProfileList;

namespace YARG.Input.Bindings
{
    public partial class ReusableBindingSet
    {
        public Guid Guid { get; private set; }
        public GameMode Mode { get; }
        public ControllerFamily ControllerFamily { get; private set; }
        public string Name { get; private set; }
        public bool IsHardcoded { get; private set; }

        // Key is binding name, like "FiveFret.Green" or "FourDrums.RedPad"
        // These names come from BindingCollection.Templates.cs; they are YARG's, not PlasticBand's
        public Dictionary<string, ReusableControlBinding> Bindings = new();

        public ReusableBindingSet(string name, GameMode mode, ControllerFamily controllerFamily, bool isHardcoded = false) {
            Name = name;
            Mode = mode;
            ControllerFamily = controllerFamily;
            Guid = isHardcoded ? Guid.Empty : Guid.NewGuid();
            IsHardcoded = isHardcoded;
        }

        public ReusableBindingSet(SerializedReusableBindingSet serialized)
        {
            Name = serialized.Name;
            Guid = serialized.Guid;
            Mode = serialized.GameMode;
            ControllerFamily = LayoutHelper.LayoutStringToControllerFamily(serialized.BaseLayout);

            var template = ReusableBindingSetTemplates.GetTemplate(Mode);

            foreach (var (key, binding) in serialized.Bindings)
            {
                if (template.TryGetValue(key, out var info))
                {
                    ReusableControlBinding newBinding = info.Type switch
                    {
                        BindingType.Button or
                        BindingType.IndividualButton or
                        BindingType.DrumButton => new ReusableButtonBinding(binding, info),
                        BindingType.Axis => new ReusableAxisBinding(binding, info),
                        BindingType.Integer => new ReusableIntegerBinding(binding, info),
                        _ => null
                    };

                    if (newBinding is not null)
                    {
                        Bindings.Add(key, newBinding);
                    }
                    else
                    {
                        YargLogger.LogWarning($"Failed to parse binding with key {key} as any known binding type");
                    }
                }
                else
                {
                    YargLogger.LogWarning($"Unrecognized input action name {key} for controller family {ControllerFamily}; ignoring");
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

            foreach (var (key, binding) in Bindings)
            {
                serializedBindings[key] = binding.Serialize();
            }

            return new SerializedReusableBindingSet(Name, LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily)) {
                Guid = Guid,
                GameMode = Mode,
                BaseLayout = LayoutHelper.ControllerFamilyToLayoutString(ControllerFamily),

                Bindings = serializedBindings
            };
        }
    }
}
