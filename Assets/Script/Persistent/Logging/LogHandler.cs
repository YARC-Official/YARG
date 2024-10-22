using System;
using System.IO;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Logging.Unity;

namespace YARG.Logging
{
    [DefaultExecutionOrder(-4000)]
    public static partial class LogHandler
    {
        private static bool _isInitialized;

        private static string _logsDirectory;

        private static FileYargLogListener _fileYargLogListener;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void SetupLogHandler()
        {
            if (_isInitialized)
            {
                return;
            }

            // We must do this as the LogHandler is initialized before the PathHelper
            var persistentPath = Application.persistentDataPath;

#if UNITY_EDITOR || YARG_TEST_BUILD
            persistentPath = PathHelper.SanitizePath(Path.Combine(persistentPath, "dev"));
#elif YARG_NIGHTLY_BUILD
            persistentPath = PathHelper.SanitizePath(Path.Combine(persistentPath, "nightly"));
#else
            persistentPath = PathHelper.SanitizePath(Path.Combine(persistentPath, "release"));
#endif

            // Persistent Data Path override passed in from CLI
            if (!string.IsNullOrWhiteSpace(CommandLineArgs.PersistentDataPath))
            {
                persistentPath = PathHelper.SanitizePath(CommandLineArgs.PersistentDataPath);
                Directory.CreateDirectory(persistentPath);
            }

            _logsDirectory = Path.Combine(persistentPath, "logs");
            Directory.CreateDirectory(_logsDirectory);

            _fileYargLogListener = new FileYargLogListener(GetLogPath());

            // Add log listeners here
            YargLogger.AddLogListener(new UnityEditorLogListener());
            YargLogger.AddLogListener(_fileYargLogListener);

            RegisterFormatters();

            UnityInternalLogWrapper.OverwriteUnityInternals();

            Application.logMessageReceivedThreaded += OnLogMessageReceived;

#if UNITY_EDITOR
            AppDomain.CurrentDomain.DomainUnload += ShutdownLogHandler;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
            AppDomain.CurrentDomain.ProcessExit += ShutdownLogHandler;
#endif

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

        public static void ShutdownLogHandler(object sender, EventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

            YargLogger.LogInfo("Disabling Logging");

            YargLogger.KillLogger();
            UnityInternalLogWrapper.RestoreUnityInternals();

            Application.logMessageReceivedThreaded -= OnLogMessageReceived;
#if UNITY_EDITOR
            AppDomain.CurrentDomain.DomainUnload -= ShutdownLogHandler;
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif

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

#if UNITY_EDITOR
        private static void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
            {
                ShutdownLogHandler(null, null);
            }
        }
#endif

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