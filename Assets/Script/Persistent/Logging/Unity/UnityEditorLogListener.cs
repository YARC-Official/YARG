using System;
using System.IO;
using System.Reflection;
using Cysharp.Text;
using UnityEngine;
using YARG.Core.Logging;
using Object = UnityEngine.Object;

namespace YARG
{
    public class UnityEditorLogListener : BaseYargLogListener
    {
        private readonly UnityInternalLog _unityInternalLog;
        private readonly UnityInternalLogException _unityInternalLogException;

        // Reference: https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Runtime/Export/Debug/Debug.bindings.cs

        public UnityEditorLogListener(IYargLogFormatter formatter) : base(formatter)
        {
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Static;

            var debugLogHandler = typeof(Debug).Assembly.GetType("UnityEngine.DebugLogHandler");
            var logMethod = debugLogHandler.GetMethod("Internal_Log", flags);
            var logExceptionMethod = debugLogHandler.GetMethod("Internal_LogException", flags);

            var deleg = logMethod.CreateDelegate(typeof(UnityInternalLog));
            var delegException = logExceptionMethod.CreateDelegate(typeof(UnityInternalLogException));

            //Debug.Log();

            _unityInternalLog = (UnityInternalLog) deleg;
            _unityInternalLogException = (UnityInternalLogException) delegException;
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder builder)
        {
            // LogType level,
            // LogOption options,
            // string msg,
            // Object obj
            _unityInternalLog(LogType.Log, LogOption.NoStacktrace, builder.ToString(), null);
            _unityInternalLogException(new Exception("hi"), null);
        }
    }

    public delegate void UnityInternalLog(LogType level, LogOption options, string msg, Object obj);
    public delegate void UnityInternalLogException(Exception ex, Object obj);
}