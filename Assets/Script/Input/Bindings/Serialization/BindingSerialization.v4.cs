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
    using SerializedMicV4 = SerializedMicV3;

    public class SerializedBindingsV4
    {
        public const int VERSION = 4;

        public int Version = VERSION;
        public Dictionary<Guid, SerializedProfileDeviceInfoV4> Profiles = new();
        public Dictionary<Guid, SerializedBindingCollectionV4> BindingCollections = new();

        [JsonConstructor]
        public SerializedBindingsV4() { }

        public SerializedBindingsV4(SerializedBindings serialized)
        {

            foreach (var (guid, profile) in serialized.Profiles)
            {
                Profiles[guid] = new SerializedProfileDeviceInfoV4(profile);
            }

            foreach (var (guid, bind) in serialized.ReusableBindingSets)
            {
                BindingCollections[guid] = new SerializedBindingCollectionV4(bind);
            }
        }

        public SerializedBindings Deserialize()
        {
            var deserialized = new SerializedBindings();

            foreach (var (guid, bind) in BindingCollections)
            {
                deserialized.ReusableBindingSets[guid] = bind.Deserialize();
            }

            foreach (var (guid, profile) in Profiles)
            {
                deserialized.Profiles[guid] = profile.Deserialize();
            }

            return deserialized;
        }

        public class SerializedProfileDeviceInfoV4
        {
            public List<SerializedControllerV4> Controllers = new();
            public List<SerializedMicV4> Microphones = new();
            public Dictionary<GameMode, SerializedModeMappingCollectionV4> ModeMappings = new();

            public Dictionary<string, Guid> MenuMappings = new(); // Key is BaseLayout

            [JsonConstructor]
            public SerializedProfileDeviceInfoV4() { }

            public SerializedProfileDeviceInfoV4(SerializedPlayerDeviceInfo serialized)
            {
                Controllers.AddRange(serialized.Controllers.Select((controller) => new SerializedControllerV4(controller)));

                foreach (var mic in serialized.Microphones)
                {
                    if (mic is not null)
                    {
                        Microphones.Add(new SerializedMicV4(mic));
                    }
                }

                foreach (var (gameMode, modeMappingCollection) in serialized.ModeMappings)
                {
                    ModeMappings[gameMode] = new(modeMappingCollection);
                }

                foreach (var (baseLayout, bindingSetGuid) in serialized.MenuMappings) {
                    MenuMappings[baseLayout] = bindingSetGuid;
                }
            }

            public SerializedPlayerDeviceInfo Deserialize()
            {
                var deserialized = new SerializedPlayerDeviceInfo();

                foreach (var mic in Microphones)
                {
                    if (mic is not null)
                    {
                        deserialized.Microphones.Add(mic.Deserialize());
                    }
                }

                deserialized.Controllers.AddRange(Controllers.Select((controller) => controller.Deserialize()));

                foreach (var (gameMode, modeMappingCollection) in ModeMappings)
                {
                    deserialized.ModeMappings[gameMode] = modeMappingCollection.Deserialize();
                }

                foreach (var (baseLayout, bindingSetGuid) in MenuMappings)
                {
                    deserialized.MenuMappings[baseLayout] = bindingSetGuid;
                }

                return deserialized;
            }
        }

        public class SerializedModeMappingCollectionV4
        {
            public Dictionary<string, Guid> MappingsByBaseLayout = new();

            [JsonConstructor]
            public SerializedModeMappingCollectionV4() { }

            public SerializedModeMappingCollectionV4(SerializedModeMappingCollection serialized)
            {
                foreach (var (baseLayout, bindingSetGuid) in serialized.MappingsByBaseLayout)
                {
                    MappingsByBaseLayout[baseLayout] = bindingSetGuid;
                }
            }

            public SerializedModeMappingCollection Deserialize()
            {
                var deserialized = new SerializedModeMappingCollection();

                foreach (var (baseLayout, serializedBindingCollection) in MappingsByBaseLayout)
                {
                    deserialized.MappingsByBaseLayout[baseLayout] = serializedBindingCollection;
                }

                return deserialized;
            }
        }

        public class SerializedBindingCollectionV4
        {
            public string Name;
            public Dictionary<string, SerializedControlBindingV4> Bindings = new();
            public GameMode GameMode;
            public string BaseLayout;

            [JsonConstructor]
            public SerializedBindingCollectionV4() { }

            public SerializedBindingCollectionV4(SerializedReusableBindingSet serialized)
            {
                Name = serialized.Name;
                GameMode = serialized.GameMode.Value;

                foreach (var (id, serializedBindings) in serialized.Bindings)
                {
                    Bindings[id] = new SerializedControlBindingV4(serializedBindings);
                }
            }

            public SerializedReusableBindingSet Deserialize()
            {
                var converted = new SerializedReusableBindingSet(Name, BaseLayout) {
                    GameMode = GameMode
                };

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

            public SerializedControlBindingV4(SerializedReusableControlBinding serialized)
            {
                foreach (var (name, value) in serialized.Parameters)
                {
                    Parameters.Add(name, value);
                }

                Controls.AddRange(serialized.Controls.Select((bind) => new SerializedInputControlV4(bind)));
            }

            public SerializedReusableControlBinding Deserialize()
            {
                var control = new SerializedReusableControlBinding();

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

    public class SerializedControllerV4
    {
        public string BaseLayout;
        public string Layout;
        public string Hash;

        [JsonConstructor]
        public SerializedControllerV4()
        {
            BaseLayout = string.Empty;
            Layout = string.Empty;
            Hash = string.Empty;
        }

        public SerializedControllerV4(SerializedInputDevice serialized)
        {
            BaseLayout = serialized.BaseLayout;
            Layout = serialized.Layout;
            Hash = serialized.Hash;
        }

        public SerializedInputDevice Deserialize() => new(BaseLayout, Layout, Hash);
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