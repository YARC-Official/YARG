using UnityEngine;
using UnityEngine.Events;

namespace LibVLCSharp
{
    public enum LogRotationMode { PreviousSession, FileSize, TimeInterval }

    public enum LogRotationInterval { Hourly, Daily, Monthly }

    public class VLCLogSettings : ScriptableObject
    {
        // The editor's Configure Global Logging button creates and manages the correctly
        // named Resources asset. Runtime code only needs its name.
        internal const string ResourceName = nameof(VLCLogSettings);

        [Tooltip("Includes verbose codec, demuxer, network, and playback-engine records from the shared LibVLC instance.")]
        public bool includeLibVLCEngineLogs = false;

        [Tooltip("Includes records from VLC Unity's native rendering integration, such as graphics backend initialization and texture handling.")]
        public bool includeNativeRenderingLogs = false;

        [Tooltip("Writes every received diagnostic record to the Unity Console. This global output does not enable diagnostics on individual components.")]
        public bool writeToUnityConsole = true;

        public bool writeToFile = false;

        [Tooltip("How to handle old log files.")]
        public LogRotationMode rotationMode = LogRotationMode.PreviousSession;

        [Tooltip("When to start a new log file. Example: A new file every day.")]
        public LogRotationInterval rotationInterval = LogRotationInterval.Daily;

        [Tooltip("Where to save the log file.")]
        public string logFilePath = "vlc_log.txt";

        [Tooltip("The maximum size of a single log file in megabytes.")]
        [Min(1)]
        public int maxFileSizeMB = 5;

        [Tooltip("How many old log files to keep before deleting the oldest.")]
        [Min(0)]
        public int maxRetainedFiles = 3;

        [Tooltip("Invoked on the Unity main thread whenever a diagnostic record is received from any enabled source.")]
        public UnityEvent<string> onLogReceived;

        private void OnValidate()
        {
            VLCUnityLogger.ReapplySettings(this);
        }
    }
}
