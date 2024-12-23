using System.IO;
using Cysharp.Text;

namespace YARG.Core.Logging
{
    public class FileYargLogListener : BaseYargLogListener
    {
        private readonly string       _file;

        private readonly FileStream   _fileStream;
        private readonly StreamWriter _writer;

        public FileYargLogListener(string file) : this(file, new DetailedYargLogFormatter())
        {
        }

        public FileYargLogListener(string file, IYargLogFormatter formatter) : base(formatter)
        {
            _file = file;

            _fileStream = new FileStream(file, FileMode.Append, FileAccess.Write, FileShare.Read);
            _writer = new StreamWriter(_fileStream);
        }

        public override void WriteLogItem(ref Utf16ValueStringBuilder output, LogItem item)
        {
            lock (_writer)
            {
                _writer.WriteLine(output.AsSpan());
                _writer.Flush();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                lock (_writer)
                {
                    _writer.Dispose();
                    _fileStream.Dispose();
                }
            }
        }
    }
}