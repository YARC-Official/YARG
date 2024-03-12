using System;
using System.IO;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Logging;
using YARG.Helpers;
using YARG.Logging.Unity;

namespace YARG.Logging
{
    [DefaultExecutionOrder(-3000)]
    public static class LogHandler
    {
        private static bool _isInitialized;

        private static string _logsDirectory;

        public static void Init()
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void SetupLogHandler()
        {
            if (_isInitialized)
            {
                return;
            }

            _logsDirectory = Path.Combine(PathHelper.PersistentDataPath, "logs");
            Directory.CreateDirectory(_logsDirectory);

            // Add log listeners here
            YargLogger.AddLogListener(new UnityEditorLogListener(new UnityEditorLogFormat()));
            YargLogger.AddLogListener(new FileYargLogListener(GetLogPath(), new StandardYargLogFormatter()));

            UnityInternalLogWrapper.OverwriteUnityInternals();

            Application.logMessageReceivedThreaded += OnLogMessageReceived;

#if UNITY_EDITOR
            AppDomain.CurrentDomain.DomainUnload += ShutdownLogHandler;
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
#else
            AppDomain.CurrentDomain.ProcessExit += ShutdownLogHandler;
#endif
            _isInitialized = true;
        }

        public static void ShutdownLogHandler(object sender, EventArgs e)
        {
            if (!_isInitialized)
            {
                return;
            }

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

                //UnityInternalLogWrapper.UnityInternalLogDelegate(type, LogOption.None, stacktrace, null);
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