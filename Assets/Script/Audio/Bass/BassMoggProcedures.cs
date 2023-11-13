using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Assets.Script.Audio.Bass
{
    public class BassMoggProcedures : FileProcedures
    {
        private readonly Stream stream;
        private readonly int start;
        private readonly long length;

        public BassMoggProcedures(Stream stream, int start)
        {
            this.stream = stream;
            this.start = start;
            this.length = stream.Length - start;
            this.stream.Seek(start, SeekOrigin.Begin);

            Close = (IntPtr) => this.stream.Close();
            Length = (IntPtr) => length;
            Read = (IntPtr Buffer, int Length, IntPtr User) =>
            {
                unsafe
                {
                    try
                    {
                        return this.stream.Read(new Span<byte>((byte*) Buffer, Length));
                    }
                    catch
                    {
                        return 0;
                    }
                }
            };
            
            Seek = (long Offset, IntPtr User) =>
            {
                try
                {
                    this.stream.Seek(Offset + this.start, SeekOrigin.Begin);
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
