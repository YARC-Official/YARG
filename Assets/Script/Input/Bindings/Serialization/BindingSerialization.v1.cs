using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;

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
                ModeMappings[gameMode] = new SerializedBindingCollectionV1(this, bindings);
            }

            if (serialized.MenuMappings is not null)
                MenuMappings = new SerializedBindingCollectionV1(this, serialized.MenuMappings);
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
                deserialized.ModeMappings[gameMode] = bindings.Deserialize(this);
            }

            if (MenuMappings is not null)
                deserialized.MenuMappings = MenuMappings.Deserialize(this);

            return deserialized;
        }
    }

    public class SerializedBindingCollectionV1
    {
        public Dictionary<string, SerializedControlBindingV1> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV1() { }

        public SerializedBindingCollectionV1(SerializedProfileBindingsV1 binds, SerializedBindingCollection serialized)
        {
            foreach (var (id, serializedBinds) in serialized.Bindings)
            {
                Bindings[id] = new SerializedControlBindingV1(binds, serializedBinds);
            }
        }

        public SerializedBindingCollection Deserialize(SerializedProfileBindingsV1 binds)
        {
            var converted = new SerializedBindingCollection();
            foreach (var (id, serializedBinds) in Bindings)
            {
                converted.Bindings[id] = serializedBinds.Deserialize(binds);
            }

            return converted;
        }
    }

    public class SerializedControlBindingV1
    {
        public List<SerializedInputControlV1> Controls = new();

        [JsonConstructor]
        public SerializedControlBindingV1() { }

        public SerializedControlBindingV1(SerializedProfileBindingsV1 binds, SerializedControlBinding serialized)
        {
            Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV1(binds, bind)));
        }

        public SerializedControlBinding Deserialize(SerializedProfileBindingsV1 binds)
        {
            var control = new SerializedControlBinding();
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

        public SerializedInputControlV1(SerializedProfileBindingsV1 binds, SerializedInputControl serialized)
        {
            int deviceIndex = binds.Devices.FindIndex(
                (device) => device.Layout == serialized.Device.Layout && device.Hash == serialized.Device.Hash);
            if (deviceIndex < 0)
                Device = new(serialized.Device);
            else
                DeviceIndex = deviceIndex;

            ControlPath = serialized.ControlPath;
            Parameters = serialized.Parameters;
        }

        public SerializedInputControl? Deserialize(SerializedProfileBindingsV1 binds)
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

            return new(Device.Deserialize(), ControlPath)
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
        private static SerializedBindingsV1 SerializeBindingsV1(SerializedBindings serialized)
        {
            return new SerializedBindingsV1(serialized);
        }

        private static SerializedBindings? DeserializeBindingsV1(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV1>();
            if (serialized is null || serialized.Version != SerializedBindingsV1.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}