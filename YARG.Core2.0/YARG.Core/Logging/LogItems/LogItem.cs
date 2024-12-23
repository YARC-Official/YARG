using System;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public abstract class LogItem : IDisposable
    {
        public LogLevel Level;

        public string Source = "";
        public string Method = "";

        public int Line = -1;

        public DateTime Time;

        public abstract void FormatMessage(ref Utf16ValueStringBuilder output);
        protected abstract void ReturnToPool();

        public void Dispose()
        {
            ReturnToPool();
        }
    }
}