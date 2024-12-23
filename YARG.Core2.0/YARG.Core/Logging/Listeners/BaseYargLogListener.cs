using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public abstract class BaseYargLogListener : IDisposable
    {
        private readonly IYargLogFormatter _formatter;

        protected BaseYargLogListener(IYargLogFormatter formatter)
        {
            _formatter = formatter;
        }

        public abstract void WriteLogItem(ref Utf16ValueStringBuilder output, LogItem item);

        public void FormatLogItem(ref Utf16ValueStringBuilder builder, LogItem item)
        {
            _formatter.FormatLogItem(ref builder, item);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Release any managed resources here
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}