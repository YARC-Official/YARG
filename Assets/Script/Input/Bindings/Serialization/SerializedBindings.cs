using System.Collections.Generic;
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

        public static SerializedInputDevice Serialize(InputDevice device)
        {
            return new()
            {
                Layout = device.layout,
                Hash = device.GetHash(),
            };
        }

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
}