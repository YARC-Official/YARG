using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Helpers;
using YARG.Player;

#nullable enable

namespace YARG.Input.Serialization
{
    // Version 0: Initial version of the bindings format.
    // v0 instead of v1 for easier handling of the case where the version field isn't present,
    // which this version doesn't contain.

    using SerializedBindingCollectionV0 = Dictionary<string, List<SerializedInputControlV0>>;
    using SerializedBindingsV0 = Dictionary<Guid, SerializedProfileBindingsV0>;
    using SerializedControlBindingV0 = List<SerializedInputControlV0>;

    public class SerializedProfileBindingsV0
    {
        public List<SerializedInputDeviceV0> Devices = new();
        public SerializedMicV0? Microphone;

        public Dictionary<GameMode, SerializedBindingCollectionV0> Bindings = new();
        public SerializedBindingCollectionV0? MenuBindings;

        [JsonConstructor]
        public SerializedProfileBindingsV0() { }

        public SerializedPlayerDeviceInfo Deserialize(Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets, string profileName)
        {
            var converted = new SerializedPlayerDeviceInfo()
            {
                Microphone = Microphone?.Deserialize(),
            };

            converted.Controllers.AddRange(Devices.Select((device) => device.Deserialize()));

            foreach (var (gameMode, bindings) in Bindings)
            {
                converted.ModeMappings[gameMode] = bindings.Deserialize(this, gameMode, Devices, reusableBindingSets, profileName);
            }

            if (MenuBindings is not null)
                converted.ModeMappings[GameMode.Menu] = MenuBindings.Deserialize(this, GameMode.Menu, Devices, reusableBindingSets, profileName);

            return converted;
        }
    }
    
    public class SerializedInputDeviceV0
    {
        public string Layout;
        public string Hash;

        [JsonConstructor]
        public SerializedInputDeviceV0()
        {
            Layout = string.Empty;
            Hash = string.Empty;
        }

        public SerializedInputDevice Deserialize() => new(LayoutHelper.LayoutStringToSourceLayout(Layout), Layout, Hash);
    }

    public class SerializedInputControlV0
    {
        public SerializedInputDeviceV0 Device;
        public string ControlPath;
        public Dictionary<string, string> Parameters = new();

        [JsonConstructor]
        public SerializedInputControlV0()
        {
            Device = new();
            ControlPath = string.Empty;
        }

        public SerializedSingleBinding Deserialize()
        {
            return new(ControlPath, LayoutHelper.LayoutStringToSourceLayout(Device.Layout))
            {
                Parameters = Parameters,
            };
        }
    }

    public class SerializedMicV0
    {
        public string DisplayName;

        [JsonConstructor]
        public SerializedMicV0()
        {
            DisplayName = string.Empty;
        }

        public SerializedMicV0(SerializedMic serialized)
        {
            DisplayName = serialized.Name;
        }

        public SerializedMic Deserialize() => new(DisplayName);
    }
    
    public static partial class BindingSerialization
    {
        private static SerializedBindings? DeserializeBindingsV0(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV0>();
            if (serialized is null)
                return null;

            return serialized.Deserialize();
        }

        public static SerializedBindings Deserialize(this SerializedBindingsV0 serialized)
        {
            var converted = new SerializedBindings();
            foreach (var (id, bind) in serialized)
            {
                var profileName = PlayerContainer.GetProfileById(id)?.Name ?? "Someone";
                converted.Profiles[id] = bind.Deserialize(converted.ReusableBindingSets, profileName);
            }

            return converted;
        }

        public static SerializedModeMappingCollection Deserialize(
            this SerializedBindingCollectionV0 serialized,
            SerializedProfileBindingsV0 binds,
            GameMode mode,
            List<SerializedInputDeviceV0> controllers,
            Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets,
            string profileName
        )
        {
            var converted = new SerializedModeMappingCollection();
            foreach (var controller in controllers)
            {
                SerializedReusableBindingSet? reusableBindingSet = null;

                foreach (var (actionId, controlBinding) in serialized)
                {
                    foreach (var singleBinding in controlBinding)
                    {
                        if (singleBinding?.Device is not null && singleBinding.Device.Hash == controller.Hash)
                        {
                            reusableBindingSet ??= new SerializedReusableBindingSet(
                                $"{profileName}'s {controller.Layout} Bindings",
                                LayoutHelper.LayoutStringToControllerFamily(controller.Layout),
                                mode
                            )
                            { Guid = Guid.NewGuid() };

                            if (singleBinding?.Device is not null && singleBinding.Device.Hash == controller.Hash)
                            {
                                reusableBindingSet.Bindings[actionId] = controlBinding.Deserialize();
                            }
                        }
                    }
                }

                // We don't want to flood a first-time upgrader with a bazillion redundant copies of the same default binding set
                if (LegacyBindingSetDeserialization.LegacyBindingSetShouldBeDeserialized(reusableBindingSet))
                {
                    converted.MappingsByControllerFamily[reusableBindingSet!.Family] = reusableBindingSet.Guid!.Value;
                    reusableBindingSets[reusableBindingSet.Guid!.Value] = reusableBindingSet;
                }
            }

            return converted;
        }

        public static SerializedReusableControlBinding Deserialize(this SerializedControlBindingV0 serialized)
        {
            var control = new SerializedReusableControlBinding();
            control.Controls.AddRange(serialized.Select((bind) => bind.Deserialize()));
            return control;
        }
    }
}