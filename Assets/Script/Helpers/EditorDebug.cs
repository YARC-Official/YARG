using System;
using System.Diagnostics;

namespace UnityEngine
{
    public static class EditorDebug
    {
        [Conditional("UNITY_EDITOR")]
        public static void Log(object message)
            => Debug.Log(message);

        [Conditional("UNITY_EDITOR")]
        public static void Log(object message, Object context)
            => Debug.Log(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogFormat(string format, params object[] args)
            => Debug.LogFormat(format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogFormat(Object context, string format, params object[] args)
            => Debug.LogFormat(context, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogFormat(LogType logType, LogOption logOptions, Object context, string format, params object[] args)
            => Debug.LogFormat(logType, logOptions, context, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogError(object message)
            => Debug.LogError(message);

        [Conditional("UNITY_EDITOR")]
        public static void LogError(object message, Object context)
            => Debug.LogError(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogErrorFormat(string format, params object[] args)
            => Debug.LogErrorFormat(format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogErrorFormat(Object context, string format, params object[] args)
            => Debug.LogErrorFormat(context, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogException(Exception exception)
            => Debug.LogException(exception);

        [Conditional("UNITY_EDITOR")]
        public static void LogException(Exception exception, Object context)
            => Debug.LogException(exception, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object message)
            => Debug.LogWarning(message);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(object message, Object context)
            => Debug.LogWarning(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarningFormat(string format, params object[] args)
            => Debug.LogWarningFormat(format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogWarningFormat(Object context, string format, params object[] args)
            => Debug.LogWarningFormat(context, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition)
            => Debug.Assert(condition);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition, Object context)
            => Debug.Assert(condition, context);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition, object message)
            => Debug.Assert(condition, message);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition, string message)
            => Debug.Assert(condition, message);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition, object message, Object context)
            => Debug.Assert(condition, message, context);

        [Conditional("UNITY_EDITOR")]
        public static void Assert(bool condition, string message, Object context)
            => Debug.Assert(condition, message, context);

        [Conditional("UNITY_EDITOR")]
        public static void AssertFormat(bool condition, string format, params object[] args)
            => Debug.AssertFormat(condition, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void AssertFormat(bool condition, Object context, string format, params object[] args)
            => Debug.AssertFormat(condition, context, format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogAssertion(object message)
            => Debug.LogAssertion(message);

        [Conditional("UNITY_EDITOR")]
        public static void LogAssertion(object message, Object context)
            => Debug.LogAssertion(message, context);

        [Conditional("UNITY_EDITOR")]
        public static void LogAssertionFormat(string format, params object[] args)
            => Debug.LogAssertionFormat(format, args);

        [Conditional("UNITY_EDITOR")]
        public static void LogAssertionFormat(Object context, string format, params object[] args)
            => Debug.LogAssertionFormat(context, format, args);
    }
}