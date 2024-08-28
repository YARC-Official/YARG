using System;
using System.IO;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Logging.Unity;

namespace YARG.Logging
{
    public static class LogHandler
    {
        private static bool _isInitialized;

        private static string _logsDirectory;

        private static FileYargLogListener _fileYargLogListener;

        public static void Initialize()
        {
            if (_isInitialized)
            {
                return;
            }

            // We must do this as the LogHandler is initialized before the PathHelper
            var persistentPath = Application.persistentDataPath;

#if UNITY_EDITOR || YARG_TEST_BUILD
            persistentPath = PathHelper.SanitizePath(Path.Combine(persistentPath, "dev"));
#else
            persistentPath = PathHelper.SanitizePath(Path.Combine(persistentPath, "release"));
#endif

            _logsDirectory = Path.Combine(persistentPath, "logs");
            Directory.CreateDirectory(_logsDirectory);

            _fileYargLogListener = new FileYargLogListener(GetLogPath());

            // Add log listeners here
            YargLogger.AddLogListener(new UnityEditorLogListener());
            YargLogger.AddLogListener(_fileYargLogListener);

            UnityInternalLogWrapper.OverwriteUnityInternals();

            Application.logMessageReceivedThreaded += OnLogMessageReceived;

            YargLogger.LogFormatInfo("System Information:\n" +
                "CPU: {0} ({1}MHz {2} Cores)\n" +
                "RAM: {3}MB\n" +
                "GPU: {4} (VRAM: {5}MB, Version: {6}, Renderer: {7})\n" +
                "OS: {8} ({9})\n",
                SystemInfo.processorType, SystemInfo.processorFrequency, SystemInfo.processorCount,
                SystemInfo.systemMemorySize,
                item5: SystemInfo.graphicsDeviceName, SystemInfo.graphicsMemorySize, item7: SystemInfo.graphicsDeviceVersion, SystemInfo.graphicsDeviceType,
                SystemInfo.operatingSystem, SystemInfo.operatingSystemFamily);
            _isInitialized = true;
        }

        public static void Uninitialize()
        {
            if (!_isInitialized)
            {
                return;
            }

            YargLogger.LogInfo("Disabling Logging");

            YargLogger.KillLogger();
            UnityInternalLogWrapper.RestoreUnityInternals();

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;

            _isInitialized = false;
        }

        private static void OnLogMessageReceived(string condition, string stacktrace, LogType type)
        {
            if (type is LogType.Assert or LogType.Error or LogType.Exception)
            {
                // Exceptions that come from YargLogger.LogException have no stacktrace as they are passed through
                // UnityEditorLogListener which omits the stacktrace from the Unity call
                if (string.IsNullOrEmpty(stacktrace))
                {
                    return;
                }

                using var builder = ZString.CreateStringBuilder();
                var output = builder; // Necessary to escape 'using variable' status and pass by ref

                using var item = FormatLogItem.MakeItem(
                    "--------------- EXCEPTION ---------------\n{0}\n{1}-----------------------------------------",
                    condition, stacktrace);

                item.FormatMessage(ref output);
                _fileYargLogListener.WriteLogItem(ref output, item);
            }
        }

        private static string GetLogPath()
        {
            var date = DateTime.Today;

            var file = $"{date:yyyy-MM-dd}";

            int i = 1;
            while (File.Exists(Path.Combine(_logsDirectory, file + ".log")))
            {
                file = $"{date:yyyy-MM-dd}_{i}";
                i++;
            }

            return Path.Combine(_logsDirectory, file + ".log");
        }
    }
}