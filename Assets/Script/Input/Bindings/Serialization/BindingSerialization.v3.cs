using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine.InputSystem;
using YARG.Core;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Player;

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

    public class SerializedProfileBindingsV3
    {
        public List<SerializedInputDeviceV3> Devices = new();
        public List<SerializedMicV3> Microphones = new();

        public Dictionary<GameMode, SerializedBindingCollectionV3> ModeMappings = new();
        public SerializedBindingCollectionV3? MenuMappings;

        [JsonConstructor]
        public SerializedProfileBindingsV3() { }

        public SerializedPlayerDeviceInfo Deserialize(Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets, string profileName)
        {
            var deserialized = new SerializedPlayerDeviceInfo();

            foreach (var mic in Microphones)
            {
                if (mic is not null)
                {
                    deserialized.Microphones.Add(mic.Deserialize());
                }
            }

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

    public class SerializedBindingCollectionV3
    {
        public Dictionary<string, SerializedControlBindingV3> Bindings = new();

        [JsonConstructor]
        public SerializedBindingCollectionV3() { }

        public SerializedModeMappingCollection Deserialize(
            SerializedProfileBindingsV3 binds,
            GameMode mode,
            List<SerializedInputDeviceV3> controllers,
            Dictionary<Guid, SerializedReusableBindingSet> reusableBindingSets,
            string profileName
        )
        {
            var converted = new SerializedModeMappingCollection();

            foreach (var controller in controllers)
            {
                var controllerSourceLayout = LayoutHelper.LayoutStringToSourceLayout(controller.Layout);

                SerializedReusableBindingSet? reusableBindingSet = null;

                foreach (var (actionId, controlBinding) in Bindings)
                {
                    foreach (var singleBinding in controlBinding.Controls)
                    {
                        var bindingLayout = InputControlPath.TryGetDeviceLayout(singleBinding.ControlPath);
                        var bindingSourceLayout = LayoutHelper.LayoutStringToSourceLayout(bindingLayout);

                        if (bindingSourceLayout == controllerSourceLayout)
                        {
                            reusableBindingSet ??= new SerializedReusableBindingSet(
                                $"{profileName}'s {controller.Layout} Bindings",
                                LayoutHelper.LayoutStringToControllerFamily(controller.Layout),
                                mode
                            ) { Guid = Guid.NewGuid() };

                            reusableBindingSet.Bindings[actionId] = controlBinding.Deserialize(binds);
                        }
                    }
                }

                // We don't want to flood a first-time upgrader with a bazillion redundant copies of the same default binding set
                if (LegacyBindingSetDeserialization.LegacyBindingSetShouldBeDeserialized(reusableBindingSet))
                {
                    converted.MappingsByControllerFamily[reusableBindingSet!.Family] = reusableBindingSet.Guid!.Value;
                    reusableBindingSets[reusableBindingSet.Guid!.Value] = reusableBindingSet;
                }
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

        public SerializedReusableControlBinding Deserialize(SerializedProfileBindingsV3 binds)
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

        public SerializedSingleBinding? Deserialize(SerializedProfileBindingsV3 binds)
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
        private static SerializedBindings? DeserializeBindingsV3(JObject obj)
        {
            var serialized = obj.ToObject<SerializedBindingsV3>();
            if (serialized is null || serialized.Version != SerializedBindingsV3.VERSION)
                return null;

            return serialized.Deserialize();
        }
    }
}
