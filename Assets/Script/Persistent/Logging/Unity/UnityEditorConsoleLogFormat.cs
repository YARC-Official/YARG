using System;
using System.IO;
using Cysharp.Text;
using YARG.Core.Logging;

namespace YARG.Logging.Unity
{
    public class UnityEditorConsoleLogFormat : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            var source = item.Source.AsSpan();
            var separator = Path.DirectorySeparatorChar;

            var assetsIndex = source.IndexOf("Assets");
            if (assetsIndex != -1)
            {
                source = source[assetsIndex..];
            }

            int lastSeparatorIndex = source.LastIndexOf(separator);
            var fileName = source[(lastSeparatorIndex + 1)..];

            output.Append("[");

            output.Append("<a href=\"");
            output.Append(source);
            output.AppendFormat("\" line=\"{0}\">", item.Line);
            output.Append(fileName);
            output.AppendFormat(":{0}:{1}</a>] ", item.Method, item.Line);

            // Append File
            //output.Append(fileName);

            // Append :Method:Line
            //output.AppendFormat(":{0}:{1}] ", item.Method, item.Line);

            item.FormatMessage(ref output);
        }
    }
}