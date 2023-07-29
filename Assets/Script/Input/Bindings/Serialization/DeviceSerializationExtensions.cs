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
        private static readonly Regex _xinputUserIndexRegex = new(@"\""userIndex\"":\d,");
        private static readonly SHA1 _hashAlgorithm = SHA1.Create();

        private static readonly Dictionary<InputDevice, string> _serialCache = new();

        public static string GetSerial(this InputDevice device)
        {
            // Use the device's serial if present
            var description = device.description;
            if (!string.IsNullOrEmpty(description.serial))
                return description.serial;

            // Check if we have a calculated serial cached already
            if (_serialCache.TryGetValue(device, out string serial))
                return serial;

            // Hash the description, best shot we have without a proper serial
            string descriptionJson = description.ToJson();
            // Exclude user index on XInput devices
            if (description.interfaceName == "XInput")
                descriptionJson = _xinputUserIndexRegex.Replace(descriptionJson, "");

            // Calculate the hash
            var descriptionBytes = Encoding.Default.GetBytes(descriptionJson);
            var hash = _hashAlgorithm.ComputeHash(descriptionBytes);
            serial = BitConverter.ToString(hash).Replace("-", "");

            // Cache the calculated serial
            _serialCache.Add(device, serial);

            return serial;
        }
    }
}