using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine.InputSystem;

namespace YARG.Input.Serialization
{
    public static class DeviceSerializationExtensions
    {
        private static readonly Regex _xinputUserIndexRegex = new(@"\\""userIndex\\"":\s*\d,");
        private static readonly SHA1 _hashAlgorithm = SHA1.Create();

        private static readonly Dictionary<InputDevice, string> _hashCache = new();

        public static SerializedInputDevice Serialize(this InputDevice device)
        {
            return SerializedInputDevice.Serialize(device);
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