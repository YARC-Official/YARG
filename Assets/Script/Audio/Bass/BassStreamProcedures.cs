using System;
using System.IO;
using ManagedBass;

namespace YARG.Audio.BASS
{
    public class BassStreamProcedures : FileProcedures
    {
        private readonly Stream _stream;
        private readonly long _start;
        private readonly long _length;

        public BassStreamProcedures(Stream stream)
        {
            _stream = stream;
            _start = stream.Position;
            _length = stream.Length - _start;

            Close = (IntPtr) => _stream.Close();
            Length = (IntPtr) => _length;
            Read = (IntPtr Buffer, int Length, IntPtr User) =>
            {
                try
                {
                    unsafe
                    {
                        return _stream.Read(new Span<byte>((byte*) Buffer, Length));
                    }
                }
                catch
                {
                    return 0;
                }
            };

            Seek = (long Offset, IntPtr User) =>
            {
                try
                {
                    _stream.Seek(Offset + _start, SeekOrigin.Begin);
                    return true;
                }
                catch
                {
                    return false;
                }
            };
        }
    }
}
