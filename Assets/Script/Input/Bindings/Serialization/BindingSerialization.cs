using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.InputSystem;
using YARG.Audio;
using YARG.Core;

namespace YARG.Input.Serialization
{
    using BindingCollection = Dictionary<string, List<SerializedInputControl>>;

    public class SerializedProfileBindings
    {
        public List<SerializedInputDevice> Devices = new();
        public SerializedMic Microphone;

        public Dictionary<GameMode, BindingCollection> Bindings = new();
        public BindingCollection MenuBindings = new();
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

    public static class BindingSerialization
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
    }
}