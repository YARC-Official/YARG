using System.Collections.Generic;
using YARG.Core;

namespace YARG.Input.Serialization
{
    public class SerializedProfileBindings
    {
        public List<string> DeviceSerials = new();
        public Dictionary<GameMode, SerializedGameModeBindings> Bindings = new();
    }

    public class SerializedGameModeBindings
    {
        public Dictionary<string, List<SerializedInputControl>> Menu = new();
        public Dictionary<string, List<SerializedInputControl>> Gameplay = new();
    }

    public class SerializedInputControl
    {
        public string DeviceSerial;
        public string ControlPath;
        public Dictionary<string, string> Parameters = new();
    }
}