using System;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace YARG.Core.Extensions
{
    public enum Endianness
    {
        Little = 0,
        Big = 1,
    };

    public static class StreamExtensions
    {
        public static TType Read<TType>(this Stream stream, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            TType value = default;
            unsafe
            {
                byte* buffer = (byte*)&value;
                if (stream.Read(new Span<byte>(buffer, sizeof(TType))) != sizeof(TType))
                {
                    throw new EndOfStreamException($"Not enough data in the stream to read {typeof(TType)} ({sizeof(TType)} bytes)!");
                }
                CorrectByteOrder<TType>(buffer, endianness);
            }
            return value;
        }

        public static bool ReadBoolean(this Stream stream)
        {
            byte b = (byte)stream.ReadByte();
            unsafe
            {
                return *(bool*)&b;
            }
        }

        public static int Read7BitEncodedInt(this Stream stream)
        {
            uint result = 0;
            byte byteReadJustNow;

            const int MaxBytesWithoutOverflow = 4;
            for (int shift = 0; shift < MaxBytesWithoutOverflow * 7; shift += 7)
            {
                byteReadJustNow = (byte) stream.ReadByte();
                result |= (byteReadJustNow & 0x7Fu) << shift;
                if (byteReadJustNow <= 0x7Fu)
                {
                    return (int) result;
                }
            }

            byteReadJustNow = (byte) stream.ReadByte();
            if (byteReadJustNow > 0b_1111u)
            {
                throw new Exception("LEB value exceeds max allowed");
            }

            result |= (uint) byteReadJustNow << MaxBytesWithoutOverflow * 7;
            return (int) result;
        }

        public static string ReadString(this Stream stream)
        {
            int length = Read7BitEncodedInt(stream);
            if (length == 0)
            {
                return string.Empty;
            }

            if (stream is UnmanagedMemoryStream unmanaged) unsafe
            {
                string str = Encoding.UTF8.GetString(unmanaged.PositionPointer, length);
                stream.Position += length;
                return str;
            }
            else if (stream is MemoryStream managed)
            {
                string str = Encoding.UTF8.GetString(managed.GetBuffer(), (int) managed.Position, length);
                stream.Position += length;
                return str;
            }

            var bytes = stream.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        public static Color ReadColor(this Stream stream)
        {
            int argb = stream.Read<int>(Endianness.Little);
            return Color.FromArgb(argb);
        }

        public static Guid ReadGuid(this Stream stream)
        {
            Span<byte> span = stackalloc byte[16];
            if (stream.Read(span) != span.Length)
            {
                throw new EndOfStreamException("Failed to read GUID, ran out of bytes!");
            }
            return new Guid(span);
        }

        public static byte[] ReadBytes(this Stream stream, int length)
        {
            byte[] buffer = new byte[length];
            if (stream.Read(buffer, 0, length) != length)
            {
                throw new EndOfStreamException($"Not enough data in the stream to read {length} bytes!");
            }
            return buffer;
        }

        public static void Write<TType>(this Stream stream, TType value, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            unsafe
            {
                byte* buffer = (byte*) &value;
                CorrectByteOrder<TType>(buffer, endianness);
                stream.Write(new Span<byte>(buffer, sizeof(TType)));
            }
        }

        public static void Write(this Stream stream, bool value)
        {
            unsafe
            {
                stream.WriteByte(*(byte*) &value);
            }
        }

        public static void Write7BitEncodedInt(this Stream stream, int value)
        {
            // Write out an int 7 bits at a time.  The high bit of the byte,
            // when on, tells reader to continue reading more bytes.
            uint v = (uint) value;   // support negative numbers
            while (v >= 0x80)
            {
                stream.WriteByte((byte) (v | 0x80));
                v >>= 7;
            }
            stream.WriteByte((byte) v);
        }

        public static unsafe void Write(this Stream stream, string value)
        {
            if (value.Length == 0)
            {
                stream.WriteByte(0);
                return;
            }

            var buffer = stackalloc byte[value.Length * 4];
            fixed (char* chars = value)
            {
                int len = Encoding.UTF8.GetBytes(chars, value.Length, buffer, value.Length * 4);
                stream.Write7BitEncodedInt(len);
                stream.Write(new ReadOnlySpan<byte>(buffer, len));
            }
        }

        public static void Write(this Stream stream, Color color)
        {
            stream.Write(color.ToArgb(), Endianness.Little);
        }

        public static void Write(this Stream stream, Guid guid)
        {
            Span<byte> span = stackalloc byte[16];
            if (!guid.TryWriteBytes(span))
            {
                throw new InvalidOperationException("Failed to write GUID bytes.");
            }
            stream.Write(span);
        }

        private static unsafe void CorrectByteOrder<TType>(byte* bytes, Endianness endianness)
            where TType : unmanaged, IComparable, IComparable<TType>, IConvertible, IEquatable<TType>, IFormattable
        {
            // Have to flip bits if the OS uses the opposite Endian
            if ((endianness == Endianness.Little) != BitConverter.IsLittleEndian)
            {
                int half = sizeof(TType) >> 1;
                for (int i = 0, j = sizeof(TType) - 1; i < half; ++i, --j)
                    (bytes[j], bytes[i]) = (bytes[i], bytes[j]);
            }
        }
    }
}
