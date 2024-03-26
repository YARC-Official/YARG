using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;

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

        public SerializedBindingsV2(SerializedBindings serialized)
        {
            foreach (var (id, bind) in serialized.Profiles)
            {
                Profiles[id] = new SerializedProfileBindingsV2(bind);
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

    public class SerializedProfileBindingsV2
    {
        public List<SerializedInputDeviceV2> Devices = new();
        public SerializedMicV2? Microphone;

        public Dictionary<GameMode, SerializedBindingCollectionV2> ModeMappings = new();
        public SerializedBindingCollectionV2? MenuMappings;

        [JsonConstructor]
        public SerializedProfileBindingsV2() { }

        public SerializedProfileBindingsV2(SerializedProfileBindings serialized)
        {
            Devices.AddRange(serialized.Devices.Select((device) => new SerializedInputDeviceV2(device)));

            if (serialized.Microphone is not null)
                Microphone = new SerializedMicV2(serialized.Microphone);

            foreach (var (gameMode, bindings) in serialized.ModeMappings)
            {
                ModeMappings[gameMode] = new SerializedBindingCollectionV2(this, bindings);
            }

            if (serialized.MenuMappings is not null)
                MenuMappings = new SerializedBindingCollectionV2(this, serialized.MenuMappings);
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

    public class SerializedBindingCollectionV2
    {
        public Dictionary<string, SerializedControlBindingV2> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV2() { }

        public SerializedBindingCollectionV2(SerializedProfileBindingsV2 binds, SerializedBindingCollection serialized)
        {
            foreach (var (id, serializedBinds) in serialized.Bindings)
            {
                Bindings[id] = new SerializedControlBindingV2(binds, serializedBinds);
            }
        }

        public SerializedBindingCollection Deserialize(SerializedProfileBindingsV2 binds)
        {
            var converted = new SerializedBindingCollection();
            foreach (var (id, serializedBinds) in Bindings)
            {
                converted.Bindings[id] = serializedBinds.Deserialize(binds);
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

        public SerializedControlBindingV2(SerializedProfileBindingsV2 binds, SerializedControlBinding serialized)
        {
            foreach (var (name, value) in serialized.Parameters)
            {
                Parameters.Add(name, value);
            }

            Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV2(binds, bind)));
        }

        public SerializedControlBinding Deserialize(SerializedProfileBindingsV2 binds)
        {
            var control = new SerializedControlBinding();

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

        public SerializedInputControlV2(SerializedProfileBindingsV2 binds, SerializedInputControl serialized)
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

        public SerializedInputControl? Deserialize(SerializedProfileBindingsV2 binds)
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
        private static SerializedBindingsV2 SerializeBindingsV2(SerializedBindings serialized)
        {
            return new SerializedBindingsV2(serialized);
        }

        private static SerializedBindings? DeserializeBindingsV2(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV2>();
            if (serialized is null || serialized.Version != SerializedBindingsV2.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}