using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
using YARG.Player;

#nullable enable

namespace YARG.Input.Serialization
{
    // Version 2: Adds parameters to control bindings.

    // Unchanged data types
    using SerializedInputDeviceV2 = SerializedInputDeviceV0;
    using SerializedMicV2 = SerializedMicV0;

    public class SerializedBindingsV2
    {
        public const int VERSION = 2;

        public int Version = VERSION;
        public Dictionary<Guid, SerializedProfileBindingsV2> Profiles = new();

        [JsonConstructor]
        public SerializedBindingsV2() { }

        public SerializedBindings Deserialize()
        {
            var deserialized = new SerializedBindings();
            foreach (var (id, bind) in Profiles)
            {
                var profileName = PlayerContainer.GetProfileById(id)?.Name ?? "Someone";
                deserialized.Profiles[id] = bind.Deserialize(deserialized.ReusableBindingSets, profileName);
            }

            return deserialized;
        }
    }

    public class SerializedProfileBindingsV2
    {
        public List<SerializedInputDeviceV2> Devices = new();
        public SerializedMicV2? Microphone;

        public Dictionary<GameMode, SerializedBindingCollectionV2> ModeMappings = new();
        public SerializedBindingCollectionV2? MenuMappings;

        [JsonConstructor]
        public SerializedProfileBindingsV2() { }


        public SerializedPlayerDeviceInfo Deserialize(Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets, string profileName)
        {
            var deserialized = new SerializedPlayerDeviceInfo()
            {
                Microphone = Microphone?.Deserialize(),
            };

            deserialized.Controllers.AddRange(Devices.Select((device) => device.Deserialize()));

            foreach (var (gameMode, bindings) in ModeMappings)
            {
                deserialized.ModeMappings[gameMode] = bindings.Deserialize(this, gameMode, Devices, reusableBindingSets, profileName);
            }

            if (MenuMappings is not null)
                deserialized.ModeMappings[GameMode.Menu] = MenuMappings.Deserialize(this, GameMode.Menu, Devices, reusableBindingSets, profileName);

            return deserialized;
        }
    }

    public class SerializedBindingCollectionV2
    {
        public Dictionary<string, SerializedControlBindingV2> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV2() { }

        public SerializedModeMappingCollection Deserialize(
            SerializedProfileBindingsV2 binds,
            GameMode mode,
            List<SerializedInputDeviceV2> controllers,
            Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets,
            string profileName
        )
        {
            var converted = new SerializedModeMappingCollection();

            foreach (var controller in controllers)
            {
                SerializedReusableBindingSet? reusableBindingSet = null;

                foreach (var (actionId, controlBinding) in Bindings)
                {
                    foreach (var singleBinding in controlBinding.Controls)
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
                                reusableBindingSet.Bindings[actionId] = controlBinding.Deserialize(binds);
                            }
                        }
                    }
                }

                // We don't want to flood a first-time upgrader with a bazillion redundant copies of the same default binding set, or really any duplicates at all
                if (reusableBindingSet is not null && !LegacyBindingSetDeserializationHelper.SerializedBindingSetIsOldDefault(reusableBindingSet))
                {
                    converted.MappingsByControllerFamily[reusableBindingSet.Family] = reusableBindingSet.Guid!.Value;
                    reusableBindingSets[reusableBindingSet.Guid!.Value] = reusableBindingSet;
                }
            }

            return converted;
        }
    }

    public class SerializedControlBindingV2
    {
        public Dictionary<string, string> Parameters = new();
        public List<SerializedInputControlV2> Controls = new();

        [JsonConstructor]
        public SerializedControlBindingV2() { }

        public SerializedReusableControlBinding Deserialize(SerializedProfileBindingsV2 binds)
        {
            var control = new SerializedReusableControlBinding();

            foreach (var (name, value) in Parameters)
            {
                control.Parameters.Add(name, value);
            }

            foreach (var bind in Controls)
            {
                var deserialized = bind.Deserialize(binds);
                if (deserialized is null)
                    continue;

                control.Controls.Add(deserialized);
            }

            return control;
        }

        public bool ShouldSerializeParameters() => Parameters.Count > 0;
    }

    public class SerializedInputControlV2
    {
        public int DeviceIndex = -1;
        public SerializedInputDeviceV2? Device;

        public string ControlPath;
        public Dictionary<string, string> Parameters = new();

        [JsonConstructor]
        public SerializedInputControlV2()
        {
            ControlPath = string.Empty;
        }

        public SerializedSingleBinding? Deserialize(SerializedProfileBindingsV2 binds)
        {
            if (DeviceIndex >= 0)
            {
                if (DeviceIndex >= binds.Devices.Count)
                {
                    YargLogger.LogFormatWarning("Device at list index {0} is not present!", DeviceIndex);
                    return null;
                }

                Device = binds.Devices[DeviceIndex];
            }
            else if (Device is null)
            {
                YargLogger.LogFormatWarning("No device specified for binding '{0}'!", ControlPath);
                return null;
            }

            return new(ControlPath, LayoutHelper.LayoutStringToSourceLayout(Device.Layout))
            {
                Parameters = Parameters,
            };
        }

        // For conditional serialization
        public bool ShouldSerializeDeviceIndex() => DeviceIndex >= 0;
        public bool ShouldSerializeDevice() => !ShouldSerializeDeviceIndex();
        public bool ShouldSerializeParameters() => Parameters.Count > 0;
    }

    public static partial class BindingSerialization
    {
        private static SerializedBindings? DeserializeBindingsV2(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV2>();
            if (serialized is null || serialized.Version != SerializedBindingsV2.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}