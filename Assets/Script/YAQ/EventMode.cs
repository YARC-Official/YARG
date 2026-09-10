using System;
using YARG.Settings;

namespace YARG
{
    /// <summary>
    /// Curated Event Mode flags controlled by YAQ via <c>settings.update</c>.
    /// </summary>
    [Serializable]
    public class EventFlags
    {
        public bool hotMic = true;
        public bool showUpNextHud = true;
        public bool skipMainMenu = true;
        public bool openDifficultySelect = true;

        public static EventFlags Defaults => new();

        public void CopyFrom(EventFlags other)
        {
            if (other == null) return;
            hotMic = other.hotMic;
            showUpNextHud = other.showUpNextHud;
            skipMainMenu = other.skipMainMenu;
            openDifficultySelect = other.openDifficultySelect;
        }
    }

    /// <summary>
    /// YAQ Event Mode — strips normal menus and drives song selection from the YAQ LAN queue.
    /// Enable via Settings → Experimental → YAQ stream, or launch with
    /// <c>-event-mode</c> / <c>-yaq-event</c> (optional <c>-yaq-url …</c>).
    /// YAQ can suspend/resume with <c>eventmode.exit</c> / <c>eventmode.enter</c> while the bridge stays connected.
    /// </summary>
    public static class EventMode
    {
        public static bool Enabled { get; set; }

        /// <summary>
        /// When true, Event Mode behaviors are paused by YAQ but the WebSocket stays up.
        /// </summary>
        public static bool Suspended { get; set; }

        public static string YaqWebSocketUrl { get; set; } =
            "ws://127.0.0.1:3000/ws?role=yarg";

        public static EventFlags Flags { get; } = EventFlags.Defaults;

        public static bool StreamConnected =>
            Enabled ||
            CommandLineArgs.YaqEvent ||
            (SettingsManager.Settings?.YaqStreamEnabled.Value ?? false);

        public static bool IsActive => StreamConnected && !Suspended;
    }
}
