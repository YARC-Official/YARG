using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Input.Bindings;
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
                var deserializedModeMappings = bindings.Deserialize(this, gameMode, Devices, reusableBindingSets, profileName);

                if (deserializedModeMappings.MappingsByControllerFamily.Count > 0)
                {
                    deserialized.ModeMappings[gameMode] = deserializedModeMappings;
                }
            }

            if (MenuMappings is not null)
            {
                var deserializedMenuMappings = MenuMappings.Deserialize(this, GameMode.Menu, Devices, reusableBindingSets, profileName);

                if (deserializedMenuMappings.MappingsByControllerFamily.Count > 0)
                {
                    deserialized.ModeMappings[GameMode.Menu] = deserializedMenuMappings;
                }
            }

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
            for (var i = 0; i < controllers.Count; i++)
            {
                var controller = controllers[i];

                SerializedReusableBindingSet? reusableBindingSet = null;

                foreach (var (actionId, controlBinding) in Bindings)
                {
                    foreach (var singleBinding in controlBinding.Controls)
                    {
                        if (singleBinding.DeviceIndex == i)
                        {
                            reusableBindingSet ??= new SerializedReusableBindingSet(
                                $"{profileName}'s {controller.Layout} Bindings",
                                LayoutHelper.LayoutStringToControllerFamily(controller.Layout),
                                mode
                            )
                            { Guid = Guid.NewGuid() };

                            if (!reusableBindingSet.Bindings.ContainsKey(actionId))
                            {
                                reusableBindingSet.Bindings[actionId] = controlBinding.Deserialize(binds);
                            }

                            var deserializedSingle = singleBinding.Deserialize(binds);
                            if (deserializedSingle is not null)
                            {
                                reusableBindingSet.Bindings[actionId].Controls.Add(deserializedSingle);
                            }
                        }
                    }
                }

                // We don't want to flood a first-time upgrader with a bazillion redundant copies of the same default binding set
                if (LegacyBindingSetDeserialization.LegacyBindingSetShouldBeDeserialized(reusableBindingSet))
                {
                    // Flesh out any empty bindings from the template so we don't get KeyNotFound exceptions later
                    var template = ReusableBindingSetTemplates.GetTemplate(mode);
                    foreach (var templateAction in template.Keys)
                    {
                        if (!reusableBindingSet!.Bindings.ContainsKey(templateAction))
                        {
                            reusableBindingSet.Bindings.Add(templateAction, new());
                        }
                    }

                    converted.MappingsByControllerFamily[reusableBindingSet!.Family] = reusableBindingSet.Guid!.Value;
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
            return new SerializedReusableControlBinding();
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

            var controlName = LegacyBindingSetDeserialization.IsolateControlName(ControlPath);
            return new(controlName, LayoutHelper.LayoutStringToSourceLayout(Device.Layout))
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