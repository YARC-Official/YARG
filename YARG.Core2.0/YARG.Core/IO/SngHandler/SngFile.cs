using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using YARG.Core.Extensions;
using YARG.Core.IO.Ini;
using YARG.Core.Logging;

namespace YARG.Core.IO
{
    /// <summary>
    /// <see href="https://github.com/mdsitton/SngFileFormat">Documentation of SNG file type</see>
    /// </summary>
    public class SngFile : IEnumerable<KeyValuePair<string, SngFileListing>>, IDisposable
    {
        public readonly AbridgedFileInfo Info;
        public readonly uint Version;
        public readonly SngMask Mask;
        public readonly IniSection Metadata;

        private readonly Dictionary<string, SngFileListing> _listings;
        private readonly YARGSongFileStream? _encryptedStream;

        private SngFile(AbridgedFileInfo info, Stream stream)
        {
            Info = info;
            Version = stream.Read<uint>(Endianness.Little);
            Mask = new SngMask(stream);
            Metadata = ReadMetadata(stream);
            _listings = ReadListings(stream);
            _encryptedStream = stream as YARGSongFileStream;
        }

        public SngFileListing this[string key] => _listings[key];
        public bool ContainsKey(string key) => _listings.ContainsKey(key);
        public bool TryGetValue(string key, out SngFileListing listing) => _listings.TryGetValue(key, out listing);

        public void Dispose()
        {
            _encryptedStream?.Dispose();
        }

        public Stream LoadStream()
        {
            return _encryptedStream != null ? _encryptedStream.Clone() : new FileStream(Info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
        }

        IEnumerator<KeyValuePair<string, SngFileListing>> IEnumerable<KeyValuePair<string, SngFileListing>>.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return _listings.GetEnumerator();
        }


        private const int BYTES_64BIT = 8;
        private const int BYTES_32BIT = 4;
        private const int BYTES_24BIT = 3;
        private const int BYTES_16BIT = 2;
        private static readonly byte[] SNGPKG = { (byte)'S', (byte) 'N', (byte) 'G', (byte)'P', (byte)'K', (byte)'G' };

        public static SngFile? TryLoadFromFile(in AbridgedFileInfo file)
        {
            try
            {
                using var filestream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                using var yargSongStream = YARGSongFileStream.TryLoad(filestream);
                if (yargSongStream != null)
                {
                    yargSongStream.Position += SNGPKG.Length;
                    return new SngFile(file, yargSongStream);
                }

                filestream.Position = 0;
                Span<byte> tag = stackalloc byte[SNGPKG.Length];
                return filestream.Read(tag) == tag.Length && tag.SequenceEqual(SNGPKG) ? new SngFile(file, filestream) : null;
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error loading {file.FullName}.");
                return null;
            }
        }

        private static unsafe IniSection ReadMetadata(Stream stream)
        {
            Dictionary<string, List<IniModifier>> modifiers = new();
            long length = stream.Read<long>(Endianness.Little) - sizeof(long);
            ulong numPairs = stream.Read<ulong>(Endianness.Little);

            using var bytes = FixedArray<byte>.Read(stream, length);
            var container = new YARGTextContainer<byte>(in bytes, null!);

            for (ulong i = 0; i < numPairs; i++)
            {
                int strLength = GetLength(ref container);
                var key = Encoding.UTF8.GetString(container.Position, strLength);
                container.Position += strLength;

                strLength = GetLength(ref container);
                var next = container.Position + strLength;
                if (SongIniHandler.SONG_INI_MODIFIERS.TryGetValue(key, out var node))
                {
                    var mod = node.CreateSngModifier(ref container, strLength);
                    if (modifiers.TryGetValue(node.OutputName, out var list))
                    {
                        list.Add(mod);
                    }
                    else
                    {
                        modifiers.Add(node.OutputName, new() { mod });
                    }
                }
                container.Position = next;
            }
            return new IniSection(modifiers);
        }

        private static Dictionary<string, SngFileListing> ReadListings(Stream stream)
        {
            long length = stream.Read<long>(Endianness.Little) - sizeof(long);
            ulong numListings = stream.Read<ulong>(Endianness.Little);

            using var buffer = FixedArray<byte>.Read(stream, length);

            Dictionary<string, SngFileListing> listings = new((int)numListings);
            unsafe
            {
                var curr = buffer.Ptr;
                var end = buffer.Ptr + length;
                for (ulong i = 0; i < numListings; i++)
                {
                    if (curr == end)
                    {
                        throw new EndOfStreamException();
                    }

                    int strlen = *curr++;
                    if (curr + strlen > end)
                    {
                        throw new EndOfStreamException();
                    }

                    string filename = Encoding.UTF8.GetString(curr, strlen);
                    curr += strlen;

                    if (curr + sizeof(long) > end)
                    {
                        throw new EndOfStreamException();
                    }

                    long fileLength = *(long*) curr;
                    curr += sizeof(long);

                    if (curr + sizeof(long) > end)
                    {
                        throw new EndOfStreamException();
                    }

                    long position = *(long*) curr;
                    curr += sizeof(long);
                    listings.Add(filename.ToLower(), new SngFileListing(filename, position, fileLength));
                }
            }
            return listings;
        }

        private static unsafe int GetLength(ref YARGTextContainer<byte> container)
        {
            if (container.Position + sizeof(int) > container.End)
            {
                throw new EndOfStreamException();
            }

            int length = *(int*)container.Position;
            container.Position += sizeof(int);
            if (container.Position + length > container.End)
            {
                throw new EndOfStreamException();
            }
            return length;
        }
    }
}
