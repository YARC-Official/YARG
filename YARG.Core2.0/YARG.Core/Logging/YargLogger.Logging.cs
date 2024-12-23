using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Logging
{
    public static partial class YargLogger
    {
        public static void LogException(Exception ex, string? message = "", [CallerFilePath] string source = "", [CallerLineNumber] int line = -1, [CallerMemberName] string member = "")
        {
            LogItem logItem = !string.IsNullOrEmpty(message)
                ? FormatLogItem.MakeItem("{0}\n{1}", message, ex)
                : FormatLogItem.MakeItem("{0}", ex);

            AddLogItemToQueue(LogLevel.Exception, source, line, member, logItem);
        }
    }
}