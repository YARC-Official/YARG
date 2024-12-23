using System;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song
{
    public struct RBAudio<TType>
        where TType : unmanaged
    {
        public static readonly RBAudio<TType> Empty = new()
        {
            Track = Array.Empty<TType>(),
            Drums = Array.Empty<TType>(),
            Bass = Array.Empty<TType>(),
            Guitar = Array.Empty<TType>(),
            Keys = Array.Empty<TType>(),
            Vocals = Array.Empty<TType>(),
            Crowd = Array.Empty<TType>(),
        };

        public TType[] Track;
        public TType[] Drums;
        public TType[] Bass;
        public TType[] Guitar;
        public TType[] Keys;
        public TType[] Vocals;
        public TType[] Crowd;

        public RBAudio(UnmanagedMemoryStream stream)
        {
            Track = ReadArray(stream);
            Drums = ReadArray(stream);
            Bass = ReadArray(stream);
            Guitar = ReadArray(stream);
            Keys = ReadArray(stream);
            Vocals = ReadArray(stream);
            Crowd = ReadArray(stream);
        }

        public readonly void Serialize(MemoryStream stream)
        {
            WriteArray(Track, stream);
            WriteArray(Drums, stream);
            WriteArray(Bass, stream);
            WriteArray(Guitar, stream);
            WriteArray(Keys, stream);
            WriteArray(Vocals, stream);
            WriteArray(Crowd, stream);
        }

        public static void WriteArray(in TType[] values, MemoryStream stream)
        {
            stream.Write(values.Length, Endianness.Little);
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    var span = new ReadOnlySpan<byte>(ptr, values.Length * sizeof(TType));
                    stream.Write(span);
                }
            }
        }

        public static TType[] ReadArray(UnmanagedMemoryStream stream)
        {
            int length = stream.Read<int>(Endianness.Little);
            if (length == 0)
            {
                return Array.Empty<TType>();
            }

            var values = new TType[length];
            unsafe
            {
                fixed (TType* ptr = values)
                {
                    var span = new Span<byte>(ptr, values.Length * sizeof(TType));
                    stream.Read(span);
                }
            }
            return values;
        }
    }
}
