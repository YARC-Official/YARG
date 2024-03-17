using Cysharp.Text;
using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Logging.Unity
{
    public class UnityEditorLogListener : BaseYargLogListener
    {
        public UnityEditorLogListener() : this(new UnityEditorConsoleLogFormat())
        {
        }

        public UnityEditorLogListener(IYargLogFormatter formatter) : base(formatter)
        {
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder builder, LogItem item)
        {
            // LogType level,
            // LogOption options,
            // string msg,
            // Object obj
            var type = item.Level switch
            {
                LogLevel.Trace     => LogType.Log,
                LogLevel.Debug     => LogType.Log,
                LogLevel.Info      => LogType.Log,
                LogLevel.Warning   => LogType.Warning,
                LogLevel.Error     => LogType.Error,
                LogLevel.Exception => LogType.Exception,
                LogLevel.Failure   => LogType.Assert,
                _                  => LogType.Log
            };
            UnityInternalLogWrapper.UnityInternalLogDelegate(type, LogOption.NoStacktrace, builder.ToString(),
                null);
        }
    }
}