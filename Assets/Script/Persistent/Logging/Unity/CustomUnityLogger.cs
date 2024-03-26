using System;
using System.Globalization;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YARG.Logging.Unity
{
    public class CustomUnityLogger : ILogger
    {
        // Reference: https://github.com/Unity-Technologies/UnityCsReference/blob/6c8a95ff127619e73519662fa497e242b898f9af/Runtime/Export/Logging/Logger.cs

        public ILogHandler logHandler    { get; set; }
        public bool        logEnabled    { get; set; }
        public LogType     filterLogType { get; set; }

        public CustomUnityLogger(ILogHandler unityDebugLogHandler)
        {
            logHandler = unityDebugLogHandler;
            logEnabled = true;

            // Don't need these as we will pass everything to the logHandler
            //filterLogType = LogType.Exception;
        }

        public bool IsLogTypeAllowed(LogType logType) => true;

        private static string GetString(object message)
        {
            if (message is null)
            {
                return "Null";
            }

            if (message is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return message.ToString();
        }

        public void Log(LogType logType, object message)
        {
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, GetString(message), null);
        }

        public void Log(LogType logType, object message, Object context)
        {
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, GetString(message),
                context);
        }

        public void Log(LogType logType, string tag, object message)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, formattedMessage, null);
        }

        public void Log(LogType logType, string tag, object message, Object context)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, formattedMessage,
                context);
        }

        public void Log(object message)
        {
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Log, LogOption.NoStacktrace, GetString(message),
                null);
        }

        public void Log(string tag, object message)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Log, LogOption.NoStacktrace, formattedMessage,
                null);
        }

        public void Log(string tag, object message, Object context)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Log, LogOption.NoStacktrace, formattedMessage,
                context);
        }

        public void LogWarning(string tag, object message)
        {
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Warning, LogOption.NoStacktrace,
                GetString(message), null);
        }

        public void LogWarning(string tag, object message, Object context)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Warning, LogOption.NoStacktrace, formattedMessage,
                context);
        }

        public void LogError(string tag, object message)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Error, LogOption.NoStacktrace, formattedMessage,
                null);
        }

        public void LogError(string tag, object message, Object context)
        {
            var formattedMessage = $"{tag}: {GetString(message)}";
            UnityInternalLogWrapper.UnityInternalLogDelegate(LogType.Error, LogOption.NoStacktrace, formattedMessage,
                context);
        }

        public void LogException(Exception exception)
        {
            UnityInternalLogWrapper.UnityInternalLogExceptionDelegate(exception, null);
        }

        public void LogException(Exception exception, Object context)
        {
            UnityInternalLogWrapper.UnityInternalLogExceptionDelegate(exception, context);
        }

        public void LogFormat(LogType logType, string format, params object[] args)
        {
            var formattedMessage = string.Format(format, args);
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, formattedMessage, null);
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            var formattedMessage = string.Format(format, args);
            UnityInternalLogWrapper.UnityInternalLogDelegate(logType, LogOption.NoStacktrace, formattedMessage, null);
        }
    }
}