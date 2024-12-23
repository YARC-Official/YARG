using Cysharp.Text;

namespace YARG.Core.Logging
{
    public interface IYargLogFormatter
    {
        void FormatLogItem(ref Utf16ValueStringBuilder output, LogItem item);
    }
}