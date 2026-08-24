using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Logging;

#nullable enable

namespace YARG.Input.Serialization
{
    // Version 3: Supports multiple microphones per profile.

    // Unchanged data types
    using SerializedInputDeviceV3 = SerializedInputDeviceV0;

    public class SerializedBindingsV3
    {
        public const int VERSION = 3;

        public int Version = VERSION;
        public Dictionary<Guid, SerializedProfileBindingsV3> Profiles = new();

        [JsonConstructor]
        public SerializedBindingsV3() { }

        public SerializedBindingsV3(SerializedBindings serialized)
        {
            foreach (var (id, bind) in serialized.Profiles)
            {
                Profiles[id] = new SerializedProfileBindingsV3(bind);
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

    public class SerializedProfileBindingsV3
    {
        public List<SerializedInputDeviceV3> Devices = new();
        public List<SerializedMicV3> Microphones = new();

        public Dictionary<GameMode, SerializedBindingCollectionV3> ModeMappings = new();
        public SerializedBindingCollectionV3? MenuMappings;

        [JsonConstructor]
        public SerializedProfileBindingsV3() { }

        public SerializedProfileBindingsV3(SerializedProfileBindings serialized)
        {
            Devices.AddRange(serialized.Devices.Select((device) => new SerializedInputDeviceV3(device)));

            foreach (var mic in serialized.Microphones)
            {
                if (mic is not null)
                {
                    Microphones.Add(new SerializedMicV3(mic));
                }
            }

            foreach (var (gameMode, bindings) in serialized.ModeMappings)
            {
                ModeMappings[gameMode] = new SerializedBindingCollectionV3(this, bindings);
            }

            if (serialized.MenuMappings is not null)
                MenuMappings = new SerializedBindingCollectionV3(this, serialized.MenuMappings);
        }

        public SerializedProfileBindings Deserialize()
        {
            var deserialized = new SerializedProfileBindings();

            foreach (var mic in Microphones)
            {
                if (mic is not null)
                {
                    deserialized.Microphones.Add(mic.Deserialize());
                }
            }

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

    public class SerializedBindingCollectionV3
    {
        public Dictionary<string, SerializedControlBindingV3> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV3() { }

        public SerializedBindingCollectionV3(SerializedProfileBindingsV3 binds, SerializedBindingCollection serialized)
        {
            foreach (var (id, serializedBinds) in serialized.Bindings)
            {
                Bindings[id] = new SerializedControlBindingV3(binds, serializedBinds);
            }
        }

        public SerializedBindingCollection Deserialize(SerializedProfileBindingsV3 binds)
        {
            var converted = new SerializedBindingCollection();
            foreach (var (id, serializedBinds) in Bindings)
            {
                converted.Bindings[id] = serializedBinds.Deserialize(binds);
            }

            return converted;
        }
    }

    public class SerializedControlBindingV3
    {
        public Dictionary<string, string> Parameters = new();
        public List<SerializedInputControlV3> Controls = new();

        [JsonConstructor]
        public SerializedControlBindingV3() { }

        public SerializedControlBindingV3(SerializedProfileBindingsV3 binds, SerializedControlBinding serialized)
        {
            foreach (var (name, value) in serialized.Parameters)
            {
                Parameters.Add(name, value);
            }

            Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV3(binds, bind)));
        }

        public SerializedControlBinding Deserialize(SerializedProfileBindingsV3 binds)
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

    public class SerializedInputControlV3
    {
        public int DeviceIndex = -1;
        public SerializedInputDeviceV3? Device;

        public string ControlPath;
        public Dictionary<string, string> Parameters = new();

        [JsonConstructor]
        public SerializedInputControlV3()
        {
            ControlPath = string.Empty;
        }

        public SerializedInputControlV3(SerializedProfileBindingsV3 binds, SerializedInputControl serialized)
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

        public SerializedInputControl? Deserialize(SerializedProfileBindingsV3 binds)
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

    public class SerializedMicV3
    {
        public string BaseName;
        public int Channel;

        public string DisplayName;

        [JsonConstructor]
        public SerializedMicV3()
        {
            BaseName = string.Empty;
            DisplayName = string.Empty;
        }

        public SerializedMicV3(SerializedMic serialized)
        {
            BaseName = serialized.BaseName;
            Channel = serialized.Channel;
            DisplayName = string.Empty;
        }

        public SerializedMic Deserialize()
        {
            if (!string.IsNullOrEmpty(BaseName))
            {
                return new SerializedMic(BaseName, Channel);
            }

            if (!string.IsNullOrEmpty(DisplayName))
            {
                if (InputDeviceInfo.TryParseDisplayName(DisplayName, out var parsedBaseName, out var parsedChannel))
                {
                    return new SerializedMic(parsedBaseName, parsedChannel);
                }

                return new SerializedMic(DisplayName, 0);
            }

            return new SerializedMic(BaseName ?? string.Empty, Channel);
        }

        public bool ShouldSerializeDisplayName() => string.IsNullOrEmpty(BaseName);
        public bool ShouldSerializeBaseName() => !string.IsNullOrEmpty(BaseName);
        public bool ShouldSerializeChannel() => !string.IsNullOrEmpty(BaseName);
    }

    public static partial class BindingSerialization
    {
        private static SerializedBindingsV3 SerializeBindingsV3(SerializedBindings serialized)
        {
            return new SerializedBindingsV3(serialized);
        }

        private static SerializedBindings? DeserializeBindingsV3(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV3>();
            if (serialized is null || serialized.Version != SerializedBindingsV3.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}
