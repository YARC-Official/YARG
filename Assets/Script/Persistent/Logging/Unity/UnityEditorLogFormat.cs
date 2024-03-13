using System;
using System.IO;
using Cysharp.Text;
using YARG.Core.Logging;

namespace YARG.Logging.Unity
{
    public class UnityEditorLogFormat : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            var source = item.Source.AsSpan();
            var separator = Path.DirectorySeparatorChar;

            int lastSeparatorIndex = source.LastIndexOf(separator);
            var fileName = source[(lastSeparatorIndex + 1)..];

            output.Append("[");

            // Append File
            output.Append(fileName);

            // Append :Method:Line
            output.AppendFormat(":{0}:{1}] ", item.Method, item.Line);

            item.FormatMessage(ref output);
        }
    }
}