using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using YARG.Core.Song;

namespace YARG.Integration
{
    /// <summary>
    /// The setlist socket's wire format, version 1. Little-endian throughout. Nothing here allocates.
    /// </summary>
    internal static class SetlistProtocol
    {
        public const ushort VERSION = 1;

        public const int CLIENT_HELLO_SIZE = 8;
        public const int SERVER_HELLO_SIZE = 17;
        public const int FRAME_HEADER_SIZE = 5;
        public const int MAX_CLIENT_FRAME = 64;
        public const int MAX_SNAPSHOT_BYTES = 64 * 1024;
        public const int MAX_STRING_BYTES = 256;

        private const int SNAPSHOT_HEADER_SIZE = 22;
        private const int MIN_ENTRY_SIZE = HashWrapper.HASH_SIZE_IN_BYTES + 4 + 3 * 2;
        private const int MAX_ENTRY_SIZE = HashWrapper.HASH_SIZE_IN_BYTES + 4 + 3 * (2 + MAX_STRING_BYTES);

        public const int MAX_SNAPSHOT_ENTRIES =
            (MAX_SNAPSHOT_BYTES - FRAME_HEADER_SIZE - SNAPSHOT_HEADER_SIZE) / MIN_ENTRY_SIZE;

        private const byte FLAG_SHOW_IN_PROGRESS = 1 << 0;
        private const byte FLAG_TRUNCATED        = 1 << 1;

        public enum FrameType : byte
        {
            Snapshot = 0x01,
            Ack      = 0x02,
            Error    = 0x7F,
        }

        public enum HelloStatus : byte
        {
            Ok              = 0,
            NoCommonVersion = 1,
            Busy            = 2,
        }

        // "YSET". It isn't the start of an HTTP method or a TLS record (0x16), so a browser's request fails on its
        // first byte.
        private static ReadOnlySpan<byte> Magic => new byte[] { 0x59, 0x53, 0x45, 0x54 };

        /// <summary>
        /// Whether what a client has sent so far could still be the start of a hello.
        /// </summary>
        public static bool IsHelloPrefix(ReadOnlySpan<byte> received)
        {
            int length = Math.Min(received.Length, Magic.Length);
            return received[..length].SequenceEqual(Magic[..length]);
        }

        /// <summary>
        /// Reads a whole client hello; check <see cref="IsHelloPrefix"/> first.
        /// </summary>
        public static void ReadClientHello(ReadOnlySpan<byte> hello, out ushort minVersion, out ushort maxVersion)
        {
            minVersion = BinaryPrimitives.ReadUInt16LittleEndian(hello[4..]);
            maxVersion = BinaryPrimitives.ReadUInt16LittleEndian(hello[6..]);
        }

        public static int WriteServerHello(Span<byte> buffer, ushort version, HelloStatus status, ulong sessionNonce)
        {
            Magic.CopyTo(buffer);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[4..], version);
            buffer[6] = (byte) status;
            BinaryPrimitives.WriteUInt64LittleEndian(buffer[7..], sessionNonce);
            BinaryPrimitives.WriteUInt16LittleEndian(buffer[15..], MAX_CLIENT_FRAME);
            return SERVER_HELLO_SIZE;
        }

        /// <summary>
        /// Reads one client frame from the front of <paramref name="received"/>. Returns its size, 0 when the frame
        /// hasn't fully arrived, or -1 when it's larger than <see cref="MAX_CLIENT_FRAME"/>.
        /// </summary>
        public static int ReadClientFrame(ReadOnlySpan<byte> received, out FrameType type,
            out ReadOnlySpan<byte> payload)
        {
            type = default;
            payload = default;
            if (received.Length < FRAME_HEADER_SIZE)
            {
                return 0;
            }

            uint length = BinaryPrimitives.ReadUInt32LittleEndian(received);
            if (length > MAX_CLIENT_FRAME)
            {
                return -1;
            }

            int size = FRAME_HEADER_SIZE + (int) length;
            if (received.Length < size)
            {
                return 0;
            }

            type = (FrameType) received[4];
            payload = received[FRAME_HEADER_SIZE..size];
            return size;
        }

        /// <summary>
        /// An Ack echoes the token of the last frame the client read. Tokens are random, so a client can only
        /// acknowledge a frame it has actually read.
        /// </summary>
        public static bool TryReadAck(ReadOnlySpan<byte> payload, out uint token)
        {
            if (payload.Length != 4)
            {
                token = 0;
                return false;
            }

            token = BinaryPrimitives.ReadUInt32LittleEndian(payload);
            return true;
        }

        /// <summary>
        /// Encodes a Snapshot frame into <paramref name="buffer"/>, which must hold <see cref="MAX_SNAPSHOT_BYTES"/>.
        /// Entries that don't fit are left out and the truncated flag is set. Returns the frame's size.
        /// </summary>
        public static int WriteSnapshot(byte[] buffer, SetlistSnapshot snapshot, uint token)
        {
            var span = buffer.AsSpan(0, MAX_SNAPSHOT_BYTES);
            int offset = FRAME_HEADER_SIZE + SNAPSHOT_HEADER_SIZE;

            int written = 0;
            while (written < snapshot.EntryCount && offset + MAX_ENTRY_SIZE <= span.Length)
            {
                ref readonly var entry = ref snapshot.Entries[written];

                var hash = entry.Hash;
                MemoryMarshal.Write(span[offset..], ref hash);
                offset += HashWrapper.HASH_SIZE_IN_BYTES;

                BinaryPrimitives.WriteUInt32LittleEndian(span[offset..], entry.LengthMs);
                offset += 4;

                offset = WriteString(span, offset, entry.Title);
                offset = WriteString(span, offset, entry.Artist);
                offset = WriteString(span, offset, entry.Album);
                written++;
            }

            byte flags = 0;
            if (snapshot.ShowInProgress)
            {
                flags |= FLAG_SHOW_IN_PROGRESS;
            }

            if (written < snapshot.TotalCount)
            {
                flags |= FLAG_TRUNCATED;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(span, (uint) (offset - FRAME_HEADER_SIZE));
            span[4] = (byte) FrameType.Snapshot;
            span[5] = snapshot.Active ? (byte) 1 : (byte) 0;
            span[6] = flags;
            BinaryPrimitives.WriteUInt32LittleEndian(span[7..], snapshot.Revision);
            BinaryPrimitives.WriteInt32LittleEndian(span[11..], snapshot.ShowInProgress ? snapshot.CurrentIndex : -1);
            BinaryPrimitives.WriteUInt32LittleEndian(span[15..], (uint) snapshot.TotalCount);
            BinaryPrimitives.WriteUInt32LittleEndian(span[19..], (uint) written);
            BinaryPrimitives.WriteUInt32LittleEndian(span[23..], token);
            return offset;
        }

        /// <summary>
        /// Writes <paramref name="value"/> as a u16 length and UTF-8, cut to <see cref="MAX_STRING_BYTES"/> on a
        /// character boundary. Lone surrogates become U+FFFD.
        /// </summary>
        /// <remarks>
        /// Hand-rolled because <c>Encoding.UTF8.GetBytes</c> throws when the destination is too small and
        /// allocates a fallback buffer for lone surrogates.
        /// </remarks>
        private static int WriteString(Span<byte> span, int offset, string value)
        {
            int lengthOffset = offset;
            offset += 2;
            int end = offset + MAX_STRING_BYTES;

            for (int i = 0; value != null && i < value.Length; i++)
            {
                int codePoint = value[i];
                if (char.IsHighSurrogate(value[i]) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                {
                    codePoint = char.ConvertToUtf32(value[i], value[i + 1]);
                    i++;
                }
                else if (char.IsSurrogate(value[i]))
                {
                    codePoint = 0xFFFD;
                }

                int size = codePoint < 0x80 ? 1 : codePoint < 0x800 ? 2 : codePoint < 0x10000 ? 3 : 4;
                if (offset + size > end)
                {
                    break;
                }

                switch (size)
                {
                    case 1:
                        span[offset] = (byte) codePoint;
                        break;
                    case 2:
                        span[offset] = (byte) (0xC0 | (codePoint >> 6));
                        span[offset + 1] = (byte) (0x80 | (codePoint & 0x3F));
                        break;
                    case 3:
                        span[offset] = (byte) (0xE0 | (codePoint >> 12));
                        span[offset + 1] = (byte) (0x80 | ((codePoint >> 6) & 0x3F));
                        span[offset + 2] = (byte) (0x80 | (codePoint & 0x3F));
                        break;
                    default:
                        span[offset] = (byte) (0xF0 | (codePoint >> 18));
                        span[offset + 1] = (byte) (0x80 | ((codePoint >> 12) & 0x3F));
                        span[offset + 2] = (byte) (0x80 | ((codePoint >> 6) & 0x3F));
                        span[offset + 3] = (byte) (0x80 | (codePoint & 0x3F));
                        break;
                }

                offset += size;
            }

            BinaryPrimitives.WriteUInt16LittleEndian(span[lengthOffset..], (ushort) (offset - lengthOffset - 2));
            return offset;
        }
    }
}
