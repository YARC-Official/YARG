using System;
using System.Diagnostics;
using System.Reflection;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace YARG.Logging.Unity
{
    public static class UnityInternalLogWrapper
    {
        // Reference: https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Runtime/Export/Debug/Debug.bindings.cs

        public static UnityInternalLog UnityInternalLogDelegate { get; private set; }

        public static UnityInternalLogException UnityInternalLogExceptionDelegate { get; private set; }

        public static UnityInternalExtractFormattedStackTrace UnityInternalExtractFormattedStackTraceDelegate { get; private set; }

        private static FieldInfo s_LoggerField;

        private static ILogger _originalUnityLogger;
        private static UnityInternalLog _nativeUnityLog;
        private static Func<LogType, string, bool> _logFilter;

        public static void OverwriteUnityInternals()
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

            var debugLogHandler = typeof(Debug).Assembly.GetType("UnityEngine.DebugLogHandler");

            var logMethod = debugLogHandler.GetMethod("Internal_Log", flags);
            var logExceptionMethod = debugLogHandler.GetMethod("Internal_LogException", flags);

            var logDelegate = logMethod.CreateDelegate(typeof(UnityInternalLog));
            var logExceptionDelegate = logExceptionMethod.CreateDelegate(typeof(UnityInternalLogException));

            var extractStackTrace = typeof(StackTraceUtility).GetMethod("ExtractFormattedStackTrace", flags);
            var extractStackTraceDelegate = extractStackTrace.CreateDelegate(typeof(UnityInternalExtractFormattedStackTrace));

            _nativeUnityLog = (UnityInternalLog) logDelegate;
            UnityInternalLogDelegate = ForwardingLog;
            UnityInternalLogExceptionDelegate = (UnityInternalLogException) logExceptionDelegate;
            UnityInternalExtractFormattedStackTraceDelegate = (UnityInternalExtractFormattedStackTrace) extractStackTraceDelegate;

            // Override internal Debug.s_Logger to our own
            var debugType = typeof(Debug);
            s_LoggerField = debugType.GetField("s_Logger", flags);

            _originalUnityLogger = (ILogger) s_LoggerField.GetValue(null);

            // We pass in the original ILogHandler to our custom logger because some parts of Unity
            // explicitly check if its their type (DebugLogHandler) for certain exception logging
            s_LoggerField.SetValue(null, new CustomUnityLogger(_originalUnityLogger.logHandler));
        }

        public static void RestoreUnityInternals()
        {
            var debugType = typeof(Debug);
            var loggerField = s_LoggerField ??
                debugType.GetField("s_Logger", BindingFlags.NonPublic | BindingFlags.Static);

            // Restore original Debug.s_Logger
            if (_originalUnityLogger is not null)
            {
                loggerField.SetValue(null, _originalUnityLogger);
            }
            else
            {
                var loggerType = debugType.Assembly.GetType("UnityEngine.Logger");
                var loggerHandlerType = debugType.Assembly.GetType("UnityEngine.DebugLogHandler");

                var handler = Activator.CreateInstance(loggerHandlerType);
                var logger = Activator.CreateInstance(loggerType, handler);

                loggerField.SetValue(null, logger);
            }

            _logFilter = null;
            UnityInternalLogDelegate = _nativeUnityLog;
        }

        public static void SetLogFilter(Func<LogType, string, bool> filter)
        {
            _logFilter = filter;
            if (filter != null && UnityInternalLogDelegate != ForwardingLog)
            {
                UnityInternalLogDelegate = ForwardingLog;
            }
            else if (filter == null)
            {
                UnityInternalLogDelegate = _nativeUnityLog;
            }
        }

        private static void ForwardingLog(LogType level, LogOption options, string msg, Object obj)
        {
            var filter = _logFilter;
            if (filter != null && filter(level, msg))
            {
                return;
            }

            _nativeUnityLog?.Invoke(level, options, msg, obj);
        }

        public delegate void UnityInternalLog(LogType level, LogOption options, string msg, Object obj);

        public delegate void UnityInternalLogException(Exception ex, Object obj);

        public delegate string UnityInternalExtractFormattedStackTrace(StackTrace stackTrace);
    }
}