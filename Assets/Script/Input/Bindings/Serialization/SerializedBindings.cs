using System.Collections.Generic;
using YARG.Core;

namespace YARG.Input.Serialization
{
    using BindingCollection = Dictionary<string, List<SerializedInputControl>>;

    public class SerializedProfileBindings
    {
        public List<string> DeviceSerials = new();

        public Dictionary<GameMode, BindingCollection> Bindings = new();
        public BindingCollection MenuBindings = new();
    }

    public class SerializedInputControl
    {
        public string DeviceSerial;
        public string ControlPath;
        public Dictionary<string, string> Parameters = new();
    }
}