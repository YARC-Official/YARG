using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using YARG.Audio;
using YARG.Core;

namespace YARG.Input.Serialization
{
    // Version 0: Initial version of the bindings format.
    // v0 instead of v1 for easier handling of the case where the version field isn't present,
    // which this version doesn't contain.

    using SerializedBindingsV0 = Dictionary<Guid, SerializedProfileBindingsV0>;
    using SerializedBindingCollectionV0 = Dictionary<string, List<SerializedInputControlV0>>;
    using SerializedControlBindingV0 = List<SerializedInputControlV0>;

    public class SerializedProfileBindingsV0
    {
        public List<SerializedInputDeviceV0> Devices = new();
        public SerializedMicV0 Microphone;

        public Dictionary<GameMode, SerializedBindingCollectionV0> Bindings = new();
        public SerializedBindingCollectionV0 MenuBindings = new();

        public SerializedProfileBindingsV0(SerializedProfileBindings serialized)
        {
            Devices.AddRange(serialized.Devices.Select((device) => new SerializedInputDeviceV0(device)));

            if (serialized.Microphone is not null)
                Microphone = new SerializedMicV0(serialized.Microphone);

            foreach (var (gameMode, bindings) in serialized.Bindings)
            {
                Bindings[gameMode] = BindingSerialization.Serialize(bindings);
            }

            MenuBindings = BindingSerialization.Serialize(serialized.MenuBindings);
        }

        public SerializedProfileBindings Deserialize()
        {
            var converted = new SerializedProfileBindings()
            {
                Microphone = Microphone?.Deserialize(),
            };

            converted.Devices.AddRange(Devices.Select((device) => device.Deserialize()));

            foreach (var (gameMode, bindings) in Bindings)
            {
                converted.Bindings[gameMode] = bindings.Deserialize();
            }

            converted.MenuBindings = MenuBindings.Deserialize();

            return converted;
        }
    }

    public class SerializedInputDeviceV0
    {
        public string Layout;
        public string Hash;

        public SerializedInputDeviceV0(SerializedInputDevice serialized)
        {
            Layout = serialized.Layout;
            Hash = serialized.Hash;
        }

        public SerializedInputDevice Deserialize() => new()
        {
            Layout = Layout,
            Hash = Hash,
        };
    }

    public class SerializedInputControlV0
    {
        public SerializedInputDeviceV0 Device;
        public string ControlPath;
        public Dictionary<string, string> Parameters = new();

        public SerializedInputControlV0(SerializedInputControl serialized)
        {
            Device = new SerializedInputDeviceV0(serialized.Device);
            ControlPath = serialized.ControlPath;
            Parameters = serialized.Parameters;
        }

        public SerializedInputControl Deserialize() => new()
        {
            Device = Device.Deserialize(),
            ControlPath = ControlPath,
            Parameters = Parameters,
        };
    }

    public class SerializedMicV0
    {
        public string DisplayName;

        public SerializedMicV0(SerializedMic serialized)
        {
            DisplayName = serialized.DisplayName;
        }

        public SerializedMic Deserialize() => new()
        {
            DisplayName = DisplayName,
        };
    }

    public static partial class BindingSerialization
    {
        private static SerializedBindingsV0 SerializeBindingsV0(SerializedBindings serialized)
        {
            return Serialize(serialized);
        }

        private static SerializedBindings DeserializeBindingsV0(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV0>();
            if (serialized is null)
                return null;

            return serialized.Deserialize();
        }

        public static SerializedBindingsV0 Serialize(SerializedBindings serialized)
        {
            var converted = new SerializedBindingsV0();
            foreach (var (id, bind) in serialized.Profiles)
            {
                converted[id] = new SerializedProfileBindingsV0(bind);
            }

            return converted;
        }

        public static SerializedBindings Deserialize(this SerializedBindingsV0 serialized)
        {
            var converted = new SerializedBindings();
            foreach (var (id, bind) in serialized)
            {
                converted.Profiles[id] = bind.Deserialize();
            }

            return converted;
        }

        public static SerializedBindingCollectionV0 Serialize(SerializedBindingCollection serialized)
        {
            var converted = new SerializedBindingCollectionV0();
            foreach (var (id, serializedBinds) in serialized.Bindings)
            {
                converted[id] = Serialize(serializedBinds);
            }

            return converted;
        }

        public static SerializedBindingCollection Deserialize(this SerializedBindingCollectionV0 serialized)
        {
            var converted = new SerializedBindingCollection();
            foreach (var (id, serializedBinds) in serialized)
            {
                converted.Bindings[id] = serializedBinds.Deserialize();
            }

            return converted;
        }

        public static SerializedControlBindingV0 Serialize(SerializedControlBinding serialized)
        {
            var control = new SerializedControlBindingV0();
            control.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV0(bind)));
            return control;
        }

        public static SerializedControlBinding Deserialize(this SerializedControlBindingV0 serialized)
        {
            var control = new SerializedControlBinding();
            control.Controls.AddRange(serialized.Select((bind) => bind.Deserialize()));
            return control;
        }
    }
}