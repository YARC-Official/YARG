using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    /// <summary>
    /// A log formatter which includes minimal little information in the output.
    /// Suitable for console/debug outputs, where too much information just makes things cluttered.
    /// </summary>
    public class BasicYargLogFormatter : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            output.AppendFormat("[{0}] ", item.Level.AsLevelString());

            switch (item.Level)
            {
                case LogLevel.Exception:
                case LogLevel.Failure:
                    // Append File
                    var source = item.Source.AsSpan();
                    int lastSeparatorIndex = source.LastIndexOfAny('\\', '/');
                    var fileName = source[(lastSeparatorIndex + 1)..];

                    output.Append("[");
                    output.Append(fileName);
                    output.AppendFormat(":{0}:{1}] ", item.Method, item.Line);
                    break;
            }

            item.FormatMessage(ref output);
        }
    }
}