using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YARG.Core;

#nullable enable

namespace YARG.Input.Serialization
{
    // Version 1: Start of versioning. Adds version number and restructures format for clarity.

    // Unchanged data types
    using SerializedInputDeviceV1 = SerializedInputDeviceV0;
    using SerializedInputControlV1 = SerializedInputControlV0;
    using SerializedMicV1 = SerializedMicV0;

    public class SerializedBindingsV1
    {
        public int Version = 1;
        public Dictionary<Guid, SerializedProfileBindingsV1> Profiles = new();

        [JsonConstructor]
        public SerializedBindingsV1() { }

        public SerializedBindingsV1(SerializedBindings serialized)
        {
            foreach (var (id, bind) in serialized.Profiles)
            {
                Profiles[id] = new SerializedProfileBindingsV1(bind);
            }
        }

        public SerializedBindings Deserialize()
        {
            var deserialized = new SerializedBindings();
            foreach (var (id, bind) in Profiles)
            {
                deserialized.Profiles[id] = bind.Deserialize();
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

        public SerializedProfileBindingsV1(SerializedProfileBindings serialized)
        {
            Devices.AddRange(serialized.Devices.Select((device) => new SerializedInputDeviceV1(device)));

            if (serialized.Microphone is not null)
                Microphone = new SerializedMicV1(serialized.Microphone);

            foreach (var (gameMode, bindings) in serialized.ModeMappings)
            {
                ModeMappings[gameMode] = new SerializedBindingCollectionV1(bindings);
            }

            if (serialized.MenuMappings is not null)
                MenuMappings = new SerializedBindingCollectionV1(serialized.MenuMappings);
        }

        public SerializedProfileBindings Deserialize()
        {
            var deserialized = new SerializedProfileBindings()
            {
                Microphone = Microphone?.Deserialize(),
            };

            deserialized.Devices.AddRange(Devices.Select((device) => device.Deserialize()));

            foreach (var (gameMode, bindings) in ModeMappings)
            {
                deserialized.ModeMappings[gameMode] = bindings.Deserialize();
            }

            if (MenuMappings is not null)
                deserialized.MenuMappings = MenuMappings.Deserialize();

            return deserialized;
        }
    }

    public class SerializedBindingCollectionV1
    {
        public Dictionary<string, SerializedControlBindingV1> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV1() { }

        public SerializedBindingCollectionV1(SerializedBindingCollection serialized)
        {
            foreach (var (id, serializedBinds) in serialized.Bindings)
            {
                Bindings[id] = new SerializedControlBindingV1(serializedBinds);
            }
        }

        public SerializedBindingCollection Deserialize()
        {
            var converted = new SerializedBindingCollection();
            foreach (var (id, serializedBinds) in Bindings)
            {
                converted.Bindings[id] = serializedBinds.Deserialize();
            }

            return converted;
        }
    }

    public class SerializedControlBindingV1
    {
        public List<SerializedInputControlV1> Controls = new();

        [JsonConstructor]
        public SerializedControlBindingV1() { }

        public SerializedControlBindingV1(SerializedControlBinding serialized)
        {
            Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV1(bind)));
        }

        public SerializedControlBinding Deserialize()
        {
            var control = new SerializedControlBinding();
            control.Controls.AddRange(Controls.Select((bind) => bind.Deserialize()));
            return control;
        }
    }

    public static partial class BindingSerialization
    {
        private static SerializedBindingsV1 SerializeBindingsV1(SerializedBindings serialized)
        {
            return new SerializedBindingsV1(serialized);
        }

        private static SerializedBindings? DeserializeBindingsV1(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV1>();
            if (serialized is null || serialized.Version != 1)
                return null;

            return serialized.Deserialize();
        }
    }
}