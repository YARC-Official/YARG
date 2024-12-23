using Cysharp.Text;

namespace YARG.Core.Logging
{
    /// <summary>
    /// A log formatter which includes only the message in the output.
    /// </summary>
    public class MessageOnlyYargLogFormatter : IYargLogFormatter
    {
        public void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            item.FormatMessage(ref output);
        }
    }
}