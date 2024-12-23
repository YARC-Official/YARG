using System;
using YARG.Core.Logging;

namespace YARG.Core.Extensions
{
    public static class MemoryExtensions
    {
        public static bool TryWriteAndAdvance(ref this Span<char> dest, ReadOnlySpan<char> source, ref int written)
        {
            if (!source.TryCopyTo(dest))
                return false;

            dest = dest[source.Length..];
            written += source.Length;
            return true;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, char value, ref int written)
        {
            if (dest.Length < 1)
                return false;

            dest[0] = value;
            dest = dest[1..];
            written++;
            return true;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, int value, ref int written,
            ReadOnlySpan<char> format = default)
        {
            bool success = value.TryFormat(dest, out int valueWritten, format);
            written += valueWritten;

            if (success)
                dest = dest[valueWritten..];

            return success;
        }

        public static bool TryWriteAndAdvance(ref this Span<char> dest, double value, ref int written,
            ReadOnlySpan<char> format = default)
        {
            bool success = value.TryFormat(dest, out int valueWritten, format);
            written += valueWritten;

            if (success)
                dest = dest[valueWritten..];

            return success;
        }

        public static string ToHexString(this byte[] buffer, bool dashes = true)
            => buffer.AsSpan().ToHexString(dashes);

        public static string ToHexString(this Memory<byte> buffer, bool dashes = true)
            => ToHexString((ReadOnlySpan<byte>)buffer.Span, dashes);

        public static string ToHexString(this Span<byte> buffer, bool dashes = true)
            => ToHexString((ReadOnlySpan<byte>)buffer, dashes);

        public static string ToHexString(this ReadOnlyMemory<byte> buffer, bool dashes = true)
            => ToHexString(buffer.Span, dashes);

        public static string ToHexString(this ReadOnlySpan<byte> buffer, bool dashes = true)
        {
            Span<char> chars = stackalloc char[(buffer.Length * 3) - 1];
            bool success = TryFormatHex(buffer, chars, out _, dashes);
            YargLogger.Assert(success, "Failed to format bytes as hex string!");
            return new string(chars);
        }

        public static bool TryFormatHex(this byte[] buffer, Span<char> destination,
            out int charsWritten, bool dashes = true)
            => TryFormatHex(buffer.AsSpan(), destination, out charsWritten, dashes);

        public static bool TryFormatHex(this Memory<byte> buffer, Span<char> destination,
            out int charsWritten, bool dashes = true)
            => TryFormatHex((ReadOnlySpan<byte>)buffer.Span, destination, out charsWritten, dashes);

        public static bool TryFormatHex(this Span<byte> buffer, Span<char> destination,
            out int charsWritten, bool dashes = true)
            => TryFormatHex((ReadOnlySpan<byte>)buffer, destination, out charsWritten, dashes);

        public static bool TryFormatHex(this ReadOnlyMemory<byte> buffer, Span<char> destination,
            out int charsWritten, bool dashes = true)
            => TryFormatHex(buffer.Span, destination, out charsWritten, dashes);

        public static bool TryFormatHex(this ReadOnlySpan<byte> buffer, Span<char> destination,
            out int charsWritten, bool dashes = true)
        {
            const string characters = "0123456789ABCDEF";

            if (buffer.IsEmpty)
            {
                charsWritten = 0;
                return true;
            }

            int charsNeeded = (buffer.Length * 3) - 1;
            if (destination.Length < charsNeeded)
            {
                // Hint as to how many characters are needed
                charsWritten = charsNeeded;
                return false;
            }

            static void WriteByte(Span<char> destination, byte value, ref int index, ref int charsWritten)
            {
                destination[index++] = characters[(value & 0xF0) >> 4];
                destination[index++] = characters[value & 0x0F];
                charsWritten += 2;
            }

            // Write first byte directly
            int destIndex = 0;
            charsWritten = 0;
            WriteByte(destination, buffer[0], ref destIndex, ref charsWritten);

            if (dashes)
            {
                for (int i = 1; i < buffer.Length; i++)
                {
                    destination[destIndex++] = '-';
                    charsWritten++;
                    WriteByte(destination, buffer[i], ref destIndex, ref charsWritten);
                }
            }
            else
            {
                for (int i = 1; i < buffer.Length; i++)
                {
                    WriteByte(destination, buffer[i], ref destIndex, ref charsWritten);
                }
            }

            return true;
        }
    }
}