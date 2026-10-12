using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Player;

#nullable enable

namespace YARG.Input.Serialization
{
    // Version 1: Start of versioning. Adds version number and restructures format for clarity.

    // Unchanged data types
    using SerializedInputDeviceV1 = SerializedInputDeviceV0;
    using SerializedMicV1 = SerializedMicV0;

    public class SerializedBindingsV1
    {
        public const int VERSION = 1;

        public int Version = VERSION;
        public Dictionary<Guid, SerializedProfileBindingsV1> Profiles = new();

        [JsonConstructor]
        public SerializedBindingsV1() { }

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

    public class SerializedProfileBindingsV1
    {
        public List<SerializedInputDeviceV1> Devices = new();
        public SerializedMicV1? Microphone;

        public Dictionary<GameMode, SerializedBindingCollectionV1> ModeMappings = new();
        public SerializedBindingCollectionV1? MenuMappings;

        [JsonConstructor]
        public SerializedProfileBindingsV1() { }

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

    public class SerializedBindingCollectionV1
    {
        public Dictionary<string, SerializedControlBindingV1> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV1() { }

        public SerializedModeMappingCollection Deserialize(
            SerializedProfileBindingsV1 binds,
            GameMode mode,
            List<SerializedInputDeviceV1> controllers,
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

    public class SerializedControlBindingV1
    {
        public List<SerializedInputControlV1> Controls = new();

        [JsonConstructor]
        public SerializedControlBindingV1() { }

        public SerializedReusableControlBinding Deserialize(SerializedProfileBindingsV1 binds)
        {
            var control = new SerializedReusableControlBinding();
            foreach (var bind in Controls)
            {
                var deserialized = bind.Deserialize(binds);
                if (deserialized is null)
                    continue;

                control.Controls.Add(deserialized);
            }

            return control;
        }
    }

    public class SerializedInputControlV1
    {
        public int DeviceIndex = -1;
        public SerializedInputDeviceV1? Device;

        public string ControlPath;
        public Dictionary<string, string> Parameters = new();

        [JsonConstructor]
        public SerializedInputControlV1()
        {
            ControlPath = string.Empty;
        }

        public SerializedSingleBinding? Deserialize(SerializedProfileBindingsV1 binds)
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
        private static SerializedBindings? DeserializeBindingsV1(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV1>();
            if (serialized is null || serialized.Version != SerializedBindingsV1.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}