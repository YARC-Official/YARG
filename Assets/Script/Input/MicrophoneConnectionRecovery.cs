using System;
using System.Collections.Generic;
using YARG.Core.Audio;

namespace YARG.Input
{
    public static class MicrophoneConnectionRecovery
    {
        public static bool DisconnectUnavailable(List<MicDevice> connected, List<SerializedMic> unresolved)
        {
            bool changed = false;
            for (int i = connected.Count - 1; i >= 0; i--)
            {
                var device = connected[i];
                if (device.IsConnected) continue;
                var identity = device.Serialize();
                connected.RemoveAt(i);
                // Equal names/channels can represent separate physical microphones.
                unresolved.Add(identity);
                device.Dispose();
                changed = true;
            }
            return changed;
        }

        public static bool Resolve(List<MicDevice> connected, List<SerializedMic> unresolved,
            Func<string, int, MicDevice> create)
        {
            bool changed = false;
            for (int i = unresolved.Count - 1; i >= 0; i--)
            {
                var identity = unresolved[i];
                var device = create(identity.BaseName, identity.Channel);
                if (device == null) continue;
                connected.Add(device);
                unresolved.RemoveAt(i);
                changed = true;
            }
            return changed;
        }
    }
}
