using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core;

namespace YARG.Input.Serialization
{
    // These classes are what the bindings will use for serialization/deserialization.
    // They are *not* what will get written to the bindings file in the end, however,
    // since bindings are versioned. These are used to separate the written format and actual loaded format,
    // and to make adding things easier, since each layer of the serialization has its own type.

    public class SerializedBindings
    {
        public Dictionary<Guid, SerializedProfileBindings> Profiles = new();
    }

    public class SerializedProfileBindings
    {
        public List<SerializedInputDevice> Devices = new();
        public SerializedMic Microphone;

        public Dictionary<GameMode, SerializedBindingCollection> ModeMappings = new();
        public SerializedBindingCollection MenuMappings = new();
    }

    public class SerializedBindingCollection
    {
        public Dictionary<string, SerializedControlBinding> Bindings = new();
    }

    public class SerializedControlBinding
    {
        public List<SerializedInputControl> Controls = new();
    }

    public class SerializedInputDevice
    {
        public string Layout;
        public string Hash;

        public bool MatchesDevice(InputDevice device)
        {
            return Layout == device.layout && Hash == device.GetHash();
        }
    }

    public class SerializedInputControl
    {
        public SerializedInputDevice Device;
        public string ControlPath;
        public Dictionary<string, string> Parameters = new();
    }

    public static partial class BindingSerialization
    {
        private static readonly SHA1 _hashAlgorithm = SHA1.Create();
        private static readonly Regex _xinputUserIndexRegex = new(@"\\""userIndex\\"":\s*\d,");

        private static readonly Dictionary<InputDevice, string> _hashCache = new();

        public static SerializedInputDevice Serialize(this InputDevice device)
        {
            return new()
            {
                Layout = device.layout,
                Hash = device.GetHash(),
            };
        }

        public static string GetHash(this InputDevice device)
        {
            // Check if we have a calculated hash cached already
            if (_hashCache.TryGetValue(device, out string hash))
                return hash;

            var description = device.description;
            string descriptionJson = description.ToJson();
            // Exclude user index on XInput devices
            if (description.interfaceName == "XInput")
                descriptionJson = _xinputUserIndexRegex.Replace(descriptionJson, "");

            // Calculate the hash
            var descriptionBytes = Encoding.Default.GetBytes(descriptionJson);
            var hashBytes = _hashAlgorithm.ComputeHash(descriptionBytes);
            hash = BitConverter.ToString(hashBytes).Replace("-", "");

            // Cache the calculated hash
            _hashCache.Add(device, hash);

            return hash;
        }

        public static void SerializeBindings(SerializedBindings bindings, string bindingsPath)
        {
            try
            {
                var serialized = SerializeBindingsV0(bindings);
                string bindingsJson = JsonConvert.SerializeObject(serialized, Formatting.Indented);
                File.WriteAllText(bindingsPath, bindingsJson);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while saving bindings!");
                Debug.LogException(ex);
            }
        }

        public static SerializedBindings DeserializeBindings(string bindingsPath)
        {
            try
            {
                if (!File.Exists(bindingsPath))
                    return null;

                string bindingsJson = File.ReadAllText(bindingsPath);
                var jObject = JObject.Parse(bindingsJson);
                return DeserializeBindingsV0(jObject);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while loading bindings!");
                Debug.LogException(ex);
                return null;
            }
        }
    }
}