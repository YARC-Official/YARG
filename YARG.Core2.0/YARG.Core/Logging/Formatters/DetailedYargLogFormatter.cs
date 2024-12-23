using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    /// <summary>
    /// A log formatter which includes as much information as possible in the log output.
    /// Suitable for file logs, where available information is prioritized above all else.
    /// </summary>
    public class DetailedYargLogFormatter : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            AppendPrologue(ref output, item);

            item.FormatMessage(ref output);

            AppendEpilogue(ref output, item);
        }

        private void AppendPrologue(ref Utf16ValueStringBuilder output, LogItem item)
        {
            // Get the file name for this log item
            var source = item.Source.AsSpan();
            int lastSeparatorIndex = source.LastIndexOfAny('\\', '/');
            var fileName = source[(lastSeparatorIndex + 1)..];

            switch (item.Level)
            {
                case LogLevel.Exception:
                    output.Append("--------------- EXCEPTION ---------------\nat ");

                    // Append File
                    output.Append(fileName);

                    output.AppendFormat(":{0}:{1}\n", item.Method, item.Line);
                    break;

                case LogLevel.Failure:
                    output.Append("--------------- FAILURE ---------------\nat ");

                    // Append File
                    output.Append(fileName);

                    output.AppendFormat(":{0}:{1}\n", item.Method, item.Line);
                    break;

                default:
                    // "[Level] [Year-Month-Day HH:MM:SS File:Method:Line] Message"
                    output.Append("[");

                    // Append Level
                    output.AppendFormat("{0}] [", item.Level.AsLevelString());

                    // Append DateTime in format "Year-Month-Day Hour:Minute:Second"
                    output.AppendFormat("{0:0000}-{1:00}-{2:00} {3:00}:{4:00}:{5:00} ",
                        item.Time.Year,
                        item.Time.Month,
                        item.Time.Day,
                        item.Time.Hour,
                        item.Time.Minute,
                        item.Time.Second);

                    // Append File
                    output.Append(fileName);

                    // Append :Method:Line
                    output.AppendFormat(":{0}:{1}] ", item.Method, item.Line);
                    break;
            }
        }

        private void AppendEpilogue(ref Utf16ValueStringBuilder output, LogItem item)
        {
            switch (item.Level)
            {
                case LogLevel.Exception:
                case LogLevel.Failure:
                    output.AppendLine("\n-----------------------------------------");
                    break;
            }
        }
    }
}