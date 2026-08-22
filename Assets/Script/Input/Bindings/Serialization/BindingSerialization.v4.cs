using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Input.Serialization
{
    // Unchanged data types
    using SerializedControllerV4 = SerializedInputDeviceV0;
    using SerializedMicV4 = SerializedMicV3;

    public class SerializedBindingsV4
    {
        public const int VERSION = 4;

        public int Version = VERSION;
        public Dictionary<Guid, SerializedProfileDeviceInfoV4> Profiles = new();
        public Dictionary<string, Guid> ControllerDefaults = new();
        public List<SerializedReusableBindingSetV4> ReusableBindingSets = new();

        [JsonConstructor]
        public SerializedBindingsV4() { }

        public SerializedBindingsV4(SerializedBindings serialized)
        {
            foreach (var (id, profile) in serialized.Profiles)
            {
                Profiles[id] = new SerializedProfileDeviceInfoV4(profile);
            }

            foreach (var (id, controllerGuid) in serialized.ControllerDefaults)
            {
                ControllerDefaults[id] = controllerGuid;
            }

            foreach (var bind in serialized.ReusableBindingSets)
            {
                ReusableBindingSets.Add(new SerializedReusableBindingSetV4(bind));
            }
        }

        public SerializedBindings Deserialize()
        {
            var deserialized = new SerializedBindings();

            foreach (var (id, profile) in Profiles)
            {
                deserialized.Profiles[id] = profile.Deserialize();
            }

            foreach (var (controllerHash, bindingSetGuid) in ControllerDefaults)
            {
                deserialized.ControllerDefaults[controllerHash] = bindingSetGuid;
            }

            foreach (var bind in ReusableBindingSets)
            {
                deserialized.ReusableBindingSets.Add(bind.Deserialize());
            }

            return deserialized;
        }

        public class SerializedProfileDeviceInfoV4
        {
            public List<SerializedControllerV4> Controllers = new();
            public List<SerializedMicV4> Microphones = new();


            // Stores the GUIDs of ReusableBindingSets that this profile likes to use by default for certain GameModes
            // Can be overridden by this profile's ControllerBindings or a controller's user-specified default bindings
            public Dictionary<GameMode, Guid> ModeMappings = new();

            // Key is a controller layout hash
            // Stores the GUIDs of ReusableBindingSets that this profile has assigned to specific controllers
            // This overrides this profile's ModeMappings as well as the device's user-specified default bindings
            public Dictionary<string, Guid> ControllerMappings = new();

            // GUID of the ReusableBindingSet that this profile uses for menu bindings
            public Guid? MenuMappings;

            [JsonConstructor]
            public SerializedProfileDeviceInfoV4() { }

            public SerializedProfileDeviceInfoV4(SerializedProfileDeviceInfo serialized)
            {
                Controllers.AddRange(serialized.Controllers.Select((controller) => new SerializedControllerV4(controller)));

                foreach (var mic in serialized.Microphones)
                {
                    if (mic is not null)
                    {
                        Microphones.Add(new SerializedMicV4(mic));
                    }
                }

                foreach (var (gameMode, bindingSetGuid) in serialized.ModeMappings)
                {
                    ModeMappings[gameMode] = bindingSetGuid;
                }

                foreach (var (controllerHash, bindingSetGuid) in serialized.ControllerMappings)
                {
                    ControllerMappings[controllerHash] = bindingSetGuid;
                }

                if (serialized.MenuMappings is not null)
                {
                    MenuMappings = serialized.MenuMappings;
                }
            }

            public SerializedProfileDeviceInfo Deserialize()
            {
                var deserialized = new SerializedProfileDeviceInfo();

                foreach (var mic in Microphones)
                {
                    if (mic is not null)
                    {
                        deserialized.Microphones.Add(mic.Deserialize());
                    }
                }

                deserialized.Controllers.AddRange(Controllers.Select((controller) => controller.Deserialize()));

                foreach (var (gameMode, bindings) in ModeMappings)
                {
                    deserialized.ModeMappings[gameMode] = ModeMappings[gameMode];
                }

                foreach (var (controllerHash, bindingSetGuid) in ControllerMappings)
                {
                    deserialized.ControllerMappings[controllerHash] = ControllerMappings[controllerHash];
                }

                if (MenuMappings is not null)
                {
                    deserialized.MenuMappings = MenuMappings;
                }

                return deserialized;
            }
        }

        public class SerializedReusableBindingSetV4
        {
            public Dictionary<string, SerializedControlBindingV4> Bindings = new();

            [JsonConstructor]
            public SerializedReusableBindingSetV4() { }

            public SerializedReusableBindingSetV4(SerializedReusableBindingSet serialized)
            {
                foreach (var (id, serializedBindings) in serialized.Bindings)
                {
                    Bindings[id] = new SerializedControlBindingV4(serializedBindings);
                }
            }

            public SerializedReusableBindingSet Deserialize()
            {
                var converted = new SerializedReusableBindingSet();
                foreach (var (id, serializedBinds) in Bindings)
                {
                    converted.Bindings[id] = serializedBinds.Deserialize();
                }

                return converted;
            }
        }

        public class SerializedControlBindingV4
        {
            public Dictionary<string, string> Parameters = new();
            public List<SerializedInputControlV4> Controls = new();

            [JsonConstructor]
            public SerializedControlBindingV4() { }

            public SerializedControlBindingV4(SerializedControlBinding serialized)
            {
                foreach (var (name, value) in serialized.Parameters)
                {
                    Parameters.Add(name, value);
                }

                Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV4(bind)));
            }

            public SerializedControlBinding Deserialize()
            {
                var control = new SerializedControlBinding();

                foreach (var (name, value) in Parameters)
                {
                    control.Parameters.Add(name, value);
                }

                foreach (var bind in Controls)
                {
                    var deserialized = bind.Deserialize();
                    if (deserialized is null)
                        continue;

                    control.Controls.Add(deserialized);
                }

                return control;
            }

            public bool ShouldSerializeParameters() => Parameters.Count > 0;
        }

        public class SerializedInputControlV4
        {
            public string ControlPath;
            public Dictionary<string, string> Parameters = new();

            [JsonConstructor]
            public SerializedInputControlV4()
            {
                ControlPath = string.Empty;
            }

            public SerializedInputControlV4(SerializedInputControl serialized)
            {
                ControlPath = serialized.ControlPath;
                Parameters = serialized.Parameters;
            }

            public SerializedInputControl? Deserialize()
            {
                return new(ControlPath)
                {
                    Parameters = Parameters,
                };
            }

            // For conditional serialization
            public bool ShouldSerializeParameters() => Parameters.Count > 0;
        }

    }
    public static partial class BindingSerialization
    {
        private static SerializedBindingsV4 SerializeBindingsV4(SerializedBindings serialized)
        {
            return new SerializedBindingsV4(serialized);
        }

        private static SerializedBindings? DeserializeBindingsV4(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV4>();
            if (serialized is null || serialized.Version != SerializedBindingsV4.VERSION)
            {
                return null;
            }

            return serialized.Deserialize();
        }
    }
}