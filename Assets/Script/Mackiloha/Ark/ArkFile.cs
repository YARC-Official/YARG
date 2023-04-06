using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // ReadOnlyCollection
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.IO;
using System.Collections;
using System.IO.Compression;

namespace Mackiloha.Ark
{
    internal class EntryOffset
    {
        public long Offset;
        public int Size;

        public EntryOffset(long offset, int size)
        {
            Offset = offset;
            Size = size;
        }
    }

    public class ArkFile : Archive
    {
        private ArkVersion _version;
        private bool _encrypted;
        private bool _xor; // 0xFF after encryption?
        private int _cryptKey = 0x295E2D5E;

        private string[] _arkPaths; // 0 = HDR
        private readonly List<OffsetArkEntry> _offsetEntries;

        private const int MAX_HDR_SIZE = 20 * 0x100000; // 20MB
        private readonly byte[] ReadBuffer = new byte[0x800];

        private ArkFile() : base()
        {
            _offsetEntries = new List<OffsetArkEntry>();
        }

        public static ArkFile Create(string hdrPath, ArkVersion version, int? key)
        {
            hdrPath = Path.GetFullPath(hdrPath);

            var ark = new ArkFile();
            ark._encrypted = key.HasValue;
            ark._xor = (version >= ArkVersion.V10); // RB4/RBVR?

            if (key.HasValue)
            {
                ark._cryptKey = key.Value;
            }

            ark._version = version;

            if ((int)version < 3)
            {
                using var _ = File.Create(hdrPath);

                // Single file, not other ark parts to add
                ark._arkPaths = new[]
                {
                    hdrPath
                };
            }
            else
            {
                // Add ark part paths
                var directory = Path.GetDirectoryName(hdrPath);
                var fileNameNoExt = Path.GetFileNameWithoutExtension(hdrPath);

                var arkExt = (fileNameNoExt.All(c => char.IsUpper(c)))
                    ? ".ARK"
                    : ".ark";

                // TODO: Add additional parts dynamically
                var arkPaths = Enumerable.Range(0, 1)
                    .Select(x => Path.Combine(directory, $"{fileNameNoExt}_{x}{arkExt}"))
                    .ToList();

                // Create directory
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                // Create ark parts
                foreach (var partPath in arkPaths)
                {
                    using var _ = File.Create(partPath);
                }

                ark._arkPaths = new[]
                {
                    hdrPath,
                    arkPaths.First()
                };
            }

            return ark;
        }

        public static ArkFile FromFile(string input)
        {
            if (input == null) throw new ArgumentNullException();

            var name = Path.GetFileNameWithoutExtension(input);
            var ext = Path.GetExtension(input).ToLower();

            if (ext == ".ark" && Regex.IsMatch(name, @"_\d+$"))
            {
                // If ark path is given, correct to hdr
                var hdrPath = Directory.GetFiles(Path.GetDirectoryName(Path.GetFullPath(input)))
                    .FirstOrDefault(x => Regex.IsMatch(name, @"(?i).hdr$"));

                input = hdrPath;
            } else if (ext == ".ark")
            {
                // Treat as Freq/Amp
                using var fs = File.OpenRead(input);
                return ParseArkHeader(input, fs);
            }

            var ms = new MemoryStream();
            using (var fs = File.OpenRead(input))
            {
                if (fs.Length > MAX_HDR_SIZE)
                    throw new Exception("HDR file is larger than 20MB");

                fs.CopyTo(ms);
            }

            ms.Seek(0, SeekOrigin.Begin);
            return ParseHeader(input, ms);
        }

        private static ArkFile ParseArkHeader(string input, Stream stream)
        {
            ArkFile ark = new ArkFile();
            ark._encrypted = false;
            ark._xor = false;
            ark._arkPaths = new[] { Path.GetFullPath(input) };

            using var ar = new AwesomeReader(stream);

            // Checks version
            int version = ar.ReadInt32();

            if (!Enum.IsDefined(typeof(ArkVersion), version))
                throw new NotSupportedException($"Unsupported ark version 0x{version:X8}");

            ark._version = (ArkVersion)version;

            // Skip entries and come back later
            var entryOffset = ar.BaseStream.Position;
            var entryCount = ar.ReadInt32();
            ar.BaseStream.Seek(entryCount * 20, SeekOrigin.Current);

            // Read string blob + string indicies
            var strings = ReadStringBlob(ar);
            int[] stringIndex = ReadStringIndicies(ar);

            // Read entries
            ar.BaseStream.Seek(entryOffset + 4, SeekOrigin.Begin);

            for (int i = 0; i < entryCount; i++)
            {
                uint offset = ar.ReadUInt32();

                int fileIdx = ar.ReadInt32();
                int dirIdx = ar.ReadInt32();

                string filePath = (fileIdx >= 0)
                    ? strings[stringIndex[fileIdx]]
                    : "";

                string direPath = (dirIdx >= 0)
                    ? strings[stringIndex[dirIdx]]
                    : "";

                uint size = ar.ReadUInt32();
                uint inflatedSize = ar.ReadUInt32();

                ark._offsetEntries.Add(new OffsetArkEntry(offset, filePath, direPath, size, inflatedSize, 0, offset));
            }

            return ark;
        }

        private static ArkFile ParseHeader(string input, Stream stream)
        {
            ArkFile ark = new ArkFile();
            ark._encrypted = false;
            ark._xor = false;

            using var ar = new AwesomeReader(stream);
            
            // Checks version
            int version = ar.ReadInt32();
            bool brokenV4 = false;
            
            if (Enum.IsDefined(typeof(ArkVersion), version))
                ark._version = (ArkVersion)version;
            else
            {
                // Decrypt stream and re-checks version
                Crypt.DTBCrypt(ar.BaseStream, version, true);
                ark._cryptKey = version; // Set crypt key

                version = ar.ReadInt32();
                ark._encrypted = true;

                if (!Enum.IsDefined(typeof(ArkVersion), version))
                {
                    version = (int)((uint)version ^ 0xFFFFFFFF);
                    ark._xor = true;

                    // Check one last time
                    if (!Enum.IsDefined(typeof(ArkVersion), version))
                        throw new Exception($"Ark version of \'{version}\' is unsupported");
                    
                    long start = ar.BaseStream.Position;
                    int b;

                    // 0xFF xor rest of stream
                    while ((b = stream.ReadByte()) > -1)
                    {
                        stream.Seek(-1, SeekOrigin.Current);
                        stream.WriteByte((byte)(b ^ 0xFF));
                    }

                    ar.BaseStream.Seek(start, SeekOrigin.Begin);
                }

                ark._version = (ArkVersion)version;
            }

            if (version >= 6)
            {
                // TODO: Save 16-byte hashes
                uint hashCount = ar.ReadUInt32();
                ar.BaseStream.Position += hashCount << 4;
            }

            uint arkFileCount = ar.ReadUInt32();
            uint arkFileSizeCount = ar.ReadUInt32(); // Should be same as ark file count

            long[] partSizes = new long[arkFileSizeCount];

            // Reads ark file sizes
            if (version != 4)
                for (int i = 0; i < partSizes.Length; i++)
                    partSizes[i] = ar.ReadUInt32();
            else
            {
                var partSizeStart = ar.BaseStream.Position;

                // Version 4 uses 64-bit sizes
                for (int i = 0; i < partSizes.Length; i++)
                    partSizes[i] = ar.ReadInt64();

                // Hacky way to check if v4 is v3/v5 hybrid (i.e. "broken")
                if (partSizes.Last() > (long)uint.MaxValue)
                {
                    brokenV4 = true;
                    ar.BaseStream.Seek(partSizeStart, SeekOrigin.Begin);

                    // Re-read sizes but as 32-bit instead
                    for (int i = 0; i < partSizes.Length; i++)
                        partSizes[i] = ar.ReadUInt32();
                }
            }

            // TODO: Verify the ark parts exist and the sizes match header listing
            if (version >= 5 || brokenV4)
            {
                // Read ark names from hdr
                uint arkPathsCount = ar.ReadUInt32();
                ark._arkPaths = new string[arkPathsCount + 1];

                string directory = Path.GetDirectoryName(input);
                ark._arkPaths[0] = input;

                var hdrFileName = Path.GetFileNameWithoutExtension(input);

                for (int i = 0; i < arkPathsCount; i++)
                {
                    ar.ReadString(); // Ehh just ignore what's in hdr. Sometimes it'll be absolute instead of relative

                    ark._arkPaths[i+1] = Path.Combine(directory, $"{hdrFileName}_{i}.ark");
                }
            }
            else
                // Make a good guess
                ark._arkPaths = GetPartNames(input, partSizes.Length);

            if (version >= 6 && version <= 9)
            {
                // TODO: Save hashes?
                uint hash2Count = ar.ReadUInt32();
                ar.BaseStream.Position += hash2Count << 2;
            }

            if (version >= 7)
            {
                // TODO: Save file collection paths
                uint fileCollectionCount = ar.ReadUInt32();
                for (int i = 0; i < fileCollectionCount; i++)
                {
                    uint fileCount = ar.ReadUInt32();
                    
                    for (int j = 0; j < fileCount; j++)
                    {
                        ar.ReadString();
                    }
                }
            }

            if (version <= 7)
            {
                // Read string blob + string indicies
                var strings = ReadStringBlob(ar);
                int[] stringIndex = ReadStringIndicies(ar);

                // Reads entries
                uint entryCount = ar.ReadUInt32();

                if (version >= 4 && !brokenV4)
                    for (int i = 0; i < entryCount; i++)
                    {
                        long entryOffset = ar.ReadInt64();
                        string filePath = strings[stringIndex[ar.ReadInt32()]];
                        string direPath = strings[stringIndex[ar.ReadInt32()]];
                        uint size = ar.ReadUInt32();
                        uint inflatedSize = ar.ReadUInt32();

                        (int partIdx, long partOffset) = GetArkOffsetForEntry(entryOffset, partSizes);
                        ark._offsetEntries.Add(new OffsetArkEntry(entryOffset, filePath, direPath, size, inflatedSize, partIdx + 1, partOffset));
                    }
                else
                    for (int i = 0; i < entryCount; i++)
                    {
                        uint entryOffset = ar.ReadUInt32();
                        string filePath = strings[stringIndex[ar.ReadInt32()]];
                        string direPath = strings[stringIndex[ar.ReadInt32()]];
                        uint size = ar.ReadUInt32();
                        uint inflatedSize = ar.ReadUInt32();

                        (int partIdx, long partOffset) = GetArkOffsetForEntry(entryOffset, partSizes);
                        ark._offsetEntries.Add(new OffsetArkEntry(entryOffset, filePath, direPath, size, inflatedSize, partIdx + 1, partOffset));
                    }
            }
            else
            {
                uint entryCount = ar.ReadUInt32();
                var flags1 = new (string path, int nextIdx)[entryCount];

                // Reads file entries
                if (version <= 9)
                    for (int i = 0; i < entryCount; i++)
                    {
                        long entryOffset = ar.ReadInt64();
                        string fullPath = ar.ReadString();
                        int flag = ar.ReadInt32();
                        uint size = ar.ReadUInt32();
                        ar.BaseStream.Position += 4; // Some kind of flag (0x‭7D401F60 or 0)

                        int lastIdx = fullPath.LastIndexOf('/');
                        string filePath = (lastIdx < 0) ? fullPath : fullPath.Remove(0, lastIdx + 1);
                        string direPath = (lastIdx < 0) ? "" : fullPath.Substring(0, lastIdx);

                        (int partIdx, long partOffset) = GetArkOffsetForEntry(entryOffset, partSizes);

                        var entry = new OffsetArkEntry(entryOffset, filePath, direPath, size, 0, partIdx + 1, partOffset);
                        ark._offsetEntries.Add(entry);
                        flags1[i] = (fullPath, flag);
                    }
                else
                    for (int i = 0; i < entryCount; i++)
                    {
                        long entryOffset = ar.ReadInt64();
                        string fullPath = ar.ReadString();
                        int flag = ar.ReadInt32();
                        uint size = ar.ReadUInt32();

                        int lastIdx = fullPath.LastIndexOf('/');
                        string filePath = (lastIdx < 0) ? fullPath : fullPath.Remove(0, lastIdx + 1);
                        string direPath = (lastIdx < 0) ? "" : fullPath.Substring(0, lastIdx);

                        (int partIdx, long partOffset) = GetArkOffsetForEntry(entryOffset, partSizes);

                        var entry = new OffsetArkEntry(entryOffset, filePath, direPath, size, 0, partIdx + 1, partOffset);
                        ark._offsetEntries.Add(entry);
                        flags1[i] = (fullPath, flag);
                    }

                // Hash offset table (not needed)
                uint entryCount2 = ar.ReadUInt32();
                ar.BaseStream.Position += entryCount2 << 2;
            }

            return ark;
        }

        private static Dictionary<int, string> ReadStringBlob(AwesomeReader ar)
        {
            var strings = new Dictionary<int, string>(); // Index, value
            uint sTableSize = ar.ReadUInt32();
            int offset = 0;
            long startPosition = ar.BaseStream.Position;

            // Reads all strings in table
            while (offset < sTableSize)
            {
                string s = ar.ReadNullString();
                strings.Add(offset, s);

                offset = (int)(ar.BaseStream.Position - startPosition);
            }

            return strings;
        }

        private static int[] ReadStringIndicies(AwesomeReader ar)
        {
            // Reads string index entries
            int[] stringIndex = new int[ar.ReadUInt32()];

            for (int i = 0; i < stringIndex.Length; i++)
                stringIndex[i] = ar.ReadInt32();

            return stringIndex;
        }

        private static (int part, long offset) GetArkOffsetForEntry(long entryOffset, long[] partSizes)
        {
            int currentIdx = 0;
            long currentOffset = 0;
            long nextOffset = partSizes.First();

            while ((entryOffset >= nextOffset)
                && (currentIdx < partSizes.Length - 1))
            {
                currentOffset = nextOffset;
                nextOffset += partSizes[++currentIdx];
            }

            return (currentIdx, (entryOffset - currentOffset));
        }

        public void WriteHeader(string path)
        {
            using var ms = new MemoryStream();

            // Write to memory then file (faster)
            WriteHeader(ms);
            File.WriteAllBytes(path, ms.ToArray());
        }

        private void WriteHeader(Stream stream)
        {
            AwesomeWriter aw = new AwesomeWriter(stream, false);

            // Writes key if encrypted
            if (_encrypted) aw.Write((int)_cryptKey);
            long hdrStart = aw.BaseStream.Position;

            // Gets lengths of ark files
            var arkSizes = GetPartSizes()
                .Select(x => x.size)
                .ToArray();

            aw.Write((int)Version);

            if ((int)Version >= 6)
            {
                // Always 1?
                aw.Write((int)1);

                // Write 16-bytes (some kind of hash or timestamp)
                aw.Write((long)-1);
                aw.Write((long)-1);
            }

            if ((int)Version >= 3)
            {
                aw.Write(arkSizes.Length);
                aw.Write(arkSizes.Length);

                // Writes ark sizes
                if (Version != ArkVersion.V4)
                {
                    foreach (var size in arkSizes)
                        aw.Write((uint)size);
                }
                else
                {
                    // v4 is 64-bit for some reason
                    foreach (var size in arkSizes)
                        aw.Write((ulong)size);
                }
            }

            // Write ark paths
            if ((int)Version >= 5)
            {
                // TODO: Use a better way to write relative path
                var prefix = ((int)Version < 9) ? "gen/" : "";

                aw.Write((int)_arkPaths.Length - 1);
                foreach (var path in _arkPaths.Skip(1))
                {
                    aw.Write((string)$"{prefix}{Path.GetFileName(path)}");
                }
            }

            // 32-bit flags?
            if ((int)Version >= 6 && (int)Version <= 9)
            {
                var writeValue = ((int)Version < 9) ? -1 : 0;
                aw.Write(arkSizes.Length);

                // Write 4-bytes for each part (some kind of flag)
                foreach (var size in arkSizes)
                {
                    aw.Write((int)writeValue);
                }
            }

            if ((int)Version >= 9)
            {
                // TODO: Re-visit for ark v7 (FME)
                var writeValue = 0;
                aw.Write(arkSizes.Length);

                // Write 4-bytes for each part (seems to always be 0)
                foreach (var size in arkSizes)
                {
                    aw.Write((int)writeValue);
                }
            }

            if ((int)Version < 9)
            {
                WriteClassicFileEntries(aw);
            }
            else
            {
                WriteNewFileEntries(aw);
            }

            if (_encrypted)
            {
                byte xor = (byte)((_xor && ((int)Version >= 10)) ? 0xFF : 0x00);

                // Encrypts HDR file
                aw.BaseStream.Seek(hdrStart, SeekOrigin.Begin);
                Crypt.DTBCrypt(aw.BaseStream, (int)_cryptKey, true, xor);
            }
        }

        private static string GetAmpPath(OffsetArkEntry entry)
                => Regex.Replace(entry.FullPath, "^./", "");

        private void WriteNewFileEntries(AwesomeWriter aw)
        {
            var entryCount = _offsetEntries.Count;

            var entriesHashed = _offsetEntries
                .OrderBy(x => x.FullPath)
                .Select(x =>
                {
                    var path = GetAmpPath(x);
                    var hash = (Version == ArkVersion.V9)
                        ? CalculateHash(path, entryCount)
                        : 0;

                    return new
                    {
                        Hash = hash,
                        Entry = x,
                        Path = path
                    };
                })
                .GroupBy(x => x.Hash)
                .OrderBy(x => x.Key);

            int entryIdx = -1;
            var hashOffsets = new Dictionary<int, int>();

            // Write entries
            aw.Write(entryCount);
            foreach (var hashEntry in entriesHashed)
            {
                var hash = hashEntry.Key;
                int prevHashIdx = -1;

                foreach (var e in hashEntry)
                {
                    var entry = e.Entry;
                    WriteNewEntry(aw, entry, prevHashIdx, Version == ArkVersion.V9);

                    prevHashIdx = ++entryIdx;
                }

                hashOffsets.Add(hash, entryIdx);
            }

            if (Version != ArkVersion.V9)
            {
                // Write single hash entry and stop
                aw.Write(1);
                aw.Write(hashOffsets[0]);
                return;
            }

            // Write hash index table
            aw.Write(entryCount);
            for (int i = 0; i < entryCount; i++)
            {
                if (hashOffsets.TryGetValue(i, out entryIdx))
                {
                    // Write index to last entry w/ hash
                    aw.Write((int)entryIdx);
                }
                else
                {
                    // No hash found, write default value
                    aw.Write((int)-1);
                }
            }
        }

        private static void WriteNewEntry(AwesomeWriter aw, OffsetArkEntry entry, int prevHash, bool extraFlag = false)
        {
            // TODO: Figure out if this hack is actually needed
            var fullPath = (extraFlag)
                ? entry.FullPath
                : GetAmpPath(entry);

            aw.Write((ulong)entry.Offset);
            aw.Write((string)fullPath);
            aw.Write((int)prevHash); // Index to previous entry w/ same hash
            aw.Write((uint)entry.Size);

            // Only ark v9 seems to have this (0x‭7D401F60 when entry size not 0‬)
            if (extraFlag)
                aw.Write((int)((entry.Size <= 0) ? 0 : 0x7D401F60));
        }

        private void WriteClassicFileEntries(AwesomeWriter aw)
        {
            // Creates and writes string blob
            var entries = _offsetEntries.OrderBy(x => x.Offset).ToList();
            byte[] blob = CreateBlob(out var strings, entries);

            // Write string offset table
            int[] stringOffsets = new int[(entries.Count * 2) + 200];

            Dictionary<string, int> tableOffsets = new Dictionary<string, int>();
            foreach (var str in strings)
            {
                int hash = CalculateHash(str.Key, stringOffsets.Length);

                // Prevents duplicate hashes
                while (stringOffsets[hash] != 0)
                {
                    hash++;
                    if (hash >= stringOffsets.Length) hash = 1; // Index of 0 is reserved for empty string
                }

                stringOffsets[hash] = str.Value;

                // Sets previous hash offset
                tableOffsets.Add(str.Key, hash);
            }

            if ((int)Version >= 3)
            {
                // Sort by hash index
                entries.Sort((x, y) =>
                {
                    int xValue = tableOffsets[x.Directory];
                    int yValue = tableOffsets[y.Directory];

                    // The directories are the same
                    if (xValue == yValue)
                    {
                        // So compare file names
                        xValue = tableOffsets[x.FileName];
                        yValue = tableOffsets[y.FileName];
                    }

                    return xValue - yValue;
                });

                // Write string blob
                aw.Write((uint)blob.Length);
                aw.Write(blob);

                // Write string indicies
                aw.Write(stringOffsets.Length);
                foreach (var offset in stringOffsets)
                    aw.Write((uint)offset);
            }

            // Write entries
            aw.Write((uint)entries.Count);

            // TODO: Check for versions below 3 or above 6
            if (Version == ArkVersion.V4 || Version == ArkVersion.V5 || Version == ArkVersion.V6)
            {
                foreach (var entry in entries)
                {
                    aw.Write((ulong)entry.Offset);
                    aw.Write((uint)tableOffsets[entry.FileName]);
                    aw.Write((uint)tableOffsets[entry.Directory]);
                    aw.Write((uint)entry.Size);
                    aw.Write((uint)entry.InflatedSize);
                }
            }
            else
            {
                foreach (var entry in entries)
                {
                    aw.Write((uint)entry.Offset);
                    aw.Write((uint)tableOffsets[entry.FileName]);
                    aw.Write((uint)tableOffsets[entry.Directory]);
                    aw.Write((uint)entry.Size);
                    aw.Write((uint)entry.InflatedSize);
                }
            }

            // Write string indicies
            if ((int)Version < 3)
            {
                // Write string blob
                aw.Write((uint)blob.Length);
                aw.Write(blob);

                // Write string indicies
                aw.Write(stringOffsets.Length);
                foreach (var offset in stringOffsets)
                    aw.Write((uint)offset);
            }
        }

        private static int CalculateHash(string str, int tableSize)
        {
            int hash = 0;
            foreach (char c in str)
            {
                hash = (hash * 0x7F) + c;
                hash -= ((hash / tableSize) * tableSize);
            }
            return hash;
        }

        private byte[] CreateBlob(out Dictionary<string, int> offsets, List<OffsetArkEntry> entries)
        {
            offsets = new Dictionary<string, int>();
            byte[] nullByte = { 0x00 };
            
            using (MemoryStream ms = new MemoryStream())
            {
                // Adds null byte
                offsets.Add("", 0);
                ms.Write(nullByte, 0, nullByte.Length);

                foreach (var entry in entries)
                {
                    // Writes directory name
                    if (!offsets.ContainsKey(entry.Directory))
                    {
                        offsets.Add(entry.Directory, (int)ms.Position);

                        byte[] data = Encoding.ASCII.GetBytes(entry.Directory);
                        ms.Write(data, 0, data.Length);
                        ms.Write(nullByte, 0, nullByte.Length);
                    }

                    // Writes file name
                    if (!offsets.ContainsKey(entry.FileName))
                    {
                        offsets.Add(entry.FileName, (int)ms.Position);

                        byte[] data = Encoding.ASCII.GetBytes(entry.FileName);
                        ms.Write(data, 0, data.Length);
                        ms.Write(nullByte, 0, nullByte.Length);
                    }
                }

                return ms.ToArray();
            }
        }

        public override void CommitChanges() => CommitChanges(true);

        public void CommitChanges(bool writeHeader)
        {
            if (!PendingChanges) return;
            
            var remainingOffsetEntries = _offsetEntries
                .Except<ArkEntry>(_pendingEntries)
                .Select(x => x as OffsetArkEntry)
                .OrderBy(x => x.Offset)
                .ToList();

            if ((int)_version < 3)
            {
                if (remainingOffsetEntries.Count != 0)
                    throw new NotSupportedException($"Can't add more files to an existing ark for version {_version}");

                _offsetEntries
                    .AddRange(_pendingEntries
                        .Select(x => new OffsetArkEntry(0, x.FileName, x.Directory, 0, 0, 0, 0)));

                // Write temp header
                WriteHeader(_arkPaths.First());

                _offsetEntries.Clear();
            }

            List<EntryOffset> GetGaps()
            {
                List<EntryOffset> offsetGaps = new List<EntryOffset>();
                long previousOffset = 0;

                foreach (var offsetEntry in remainingOffsetEntries)
                {
                    if (offsetEntry.Offset - previousOffset == 0)
                    {
                        // No gap, continues
                        previousOffset = offsetEntry.Offset + offsetEntry.Size;
                        continue;
                    }

                    // Adds gap to list
                    long gapOffset = previousOffset;
                    int gapSize = (int)(offsetEntry.Offset - previousOffset);
                    offsetGaps.Add(new EntryOffset(gapOffset, gapSize));

                    previousOffset = offsetEntry.Offset + offsetEntry.Size;
                }

                return offsetGaps;
            }

            void CopyToArchive(string arkFile, long arkOffset, string entryFile)
            {
                // TODO: Extract out of this
                using (FileStream fsArk = File.OpenWrite(arkFile))
                {
                    fsArk.Seek(arkOffset, SeekOrigin.Begin);

                    using (FileStream fsEntry = File.OpenRead(entryFile))
                    {
                        fsEntry.CopyTo(fsArk);
                    }
                }
            }

            // TODO: Compare previousOffset to ark file size
            List<EntryOffset> gaps = GetGaps();
            var pendingEntries = _pendingEntries.Select(x => new { Length = new FileInfo(x.LocalFilePath).Length, Entry = x }).OrderBy(x => x.Length);

            // Gets lengths of ark files
            // TODO: Also check if part is encrypted
            var arkSizes = GetPartSizes()
                .Select(x => x.size)
                .ToArray();

            foreach (var pending in pendingEntries)
            {
                // Looks at smallest gaps first, selects first fit
                var bestFit = gaps.OrderBy(x => x.Size).FirstOrDefault(x => x.Size >= pending.Length);

                if (arkSizes.Length > 1)
                    bestFit = null; // TODO: Update for multi-part arks

                if (bestFit == null)
                {
                    // Adds to end of last archive file
                    var lastEntry = remainingOffsetEntries
                        .OrderByDescending(x => x.Offset + x.Size) // Ensures 0-length files don't conflict w/ regular files at same offset
                        .FirstOrDefault();

                    long offset = (lastEntry != null)
                        ? lastEntry.Offset + lastEntry.Size
                        : Version switch
                        {
                            ArkVersion.V2 => arkSizes.First(),
                            _ => 0
                        };

                    long partOffset = offset - arkSizes
                        .Reverse()
                        .Skip(1)
                        .Sum();

                    // Copies entry to ark file
                    CopyToArchive(_arkPaths.Last(), partOffset, pending.Entry.LocalFilePath);

                    // Get inflate size if v2 or lower
                    var inflateSize = GetInflateSize(pending.Entry.LocalFilePath);

                    // Adds ark offset entry
                    remainingOffsetEntries.Add(new OffsetArkEntry(offset, pending.Entry.FileName, pending.Entry.Directory, (uint)pending.Length, inflateSize, arkSizes.Length, partOffset));
                }
                else
                {
                    (int partIdx, long partOffset) = GetArkOffsetForEntry(bestFit.Offset, arkSizes);

                    // Copies entry to ark file (TODO: Calculate arkPath beforehand)
                    CopyToArchive(_arkPaths[partIdx + 1], partOffset, pending.Entry.LocalFilePath);

                    // Get inflate size if v2 or lower
                    var inflateSize = GetInflateSize(pending.Entry.LocalFilePath);

                    // Adds ark offset entry
                    remainingOffsetEntries.Add(new OffsetArkEntry(bestFit.Offset, pending.Entry.FileName, pending.Entry.Directory, (uint)pending.Length, inflateSize, partIdx + 1, partOffset));

                    // Updates gap entry
                    if (bestFit.Size == pending.Length)
                    {
                        // Remove gap
                        gaps.Remove(bestFit);
                    }
                    else
                    {
                        // Updates values
                        bestFit.Offset += pending.Length;
                        bestFit.Size -= (int)pending.Length;
                    }
                }
            }

            // Updates archive entries
            _pendingEntries.Clear();
            _offsetEntries.Clear();
            _offsetEntries.AddRange(remainingOffsetEntries);

            // Re-writes header file
            if (writeHeader && (int)Version >= 3)
                WriteHeader(_arkPaths[0]);
            else if ((int)Version < 3)
            {
                // Update header
                using var fs = File.OpenWrite(_arkPaths.First());
                WriteHeader(fs);
            }

            // TODO: Add an output log
        }

        protected uint GetInflateSize(string path)
        {
            if ((int)Version >= 3)
            {
                // Don't care when version is 3 or above
                return 0;
            }
            else if (Regex.IsMatch(path, "(?i).gz$"))
            {
                return GetGZipInflateSize(path);
            }
            else if (path.EndsWith(".z") || path.EndsWith(".Z"))
            {
                return GetZlibInflateSize(path);
            }
            else
            {
                return 0;
            }
        }

        protected uint GetGZipInflateSize(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                var ar = new AwesomeReader(fs);

                // Read gz magic
                var magic = ar.ReadUInt16();
                if (magic != 0x8B1F)
                    return 0;

                // Read inflate size
                fs.Seek(-4, SeekOrigin.End);
                return ar.ReadUInt32();
            }
            catch
            {
                return 0;
            }
        }

        protected uint GetZlibInflateSize(string path)
        {
            try
            {
                using var fs = File.OpenRead(path);
                using var outZStream = new DeflateStream(fs, CompressionMode.Decompress, true);

                uint size = 0;
                int read;
                while (true)
                {
                    read = outZStream.Read(ReadBuffer, 0, ReadBuffer.Length);
                    if (read <= 0)
                        break;

                    size += (uint)read;
                }

                return size;
            }
            catch
            {
                return 0;
            }
        }

        protected (long size, bool encrypted)[] GetPartSizes()
        {
            if ((int)Version < 3)
            {
                return _arkPaths
                    .Take(1)
                    .Select(x => (new FileInfo(x).Length, false))
                    .ToArray();
            }
            else if ((int)Version < 10)
            {
                return _arkPaths
                    .Skip(1)
                    .Select(x => (new FileInfo(x).Length, false))
                    .ToArray();
            }

            return _arkPaths
                .Skip(1)
                .Select(GetPartSizeAndEncryption)
                .ToArray();
        }

        protected (long size, bool encrypted) GetPartSizeAndEncryption(string partPath)
        {
            const string append = "mcnxyxcmvmcxyxcmskdldkjshagsdhfj";
            using var file = File.OpenRead(partPath);

            if (file.Length < (append.Length + 4))
                return (file.Length, false);

            using var ar = new AwesomeReader(file);
            ar.BaseStream.Seek(-append.Length, SeekOrigin.End);
            var endString = ar.ReadStringWithLength(append.Length);

            if (endString != append)
                return (file.Length, false);

            ar.BaseStream.Seek(-(append.Length + 4), SeekOrigin.End);
            int encDataLength = ar.ReadInt32();

            return (file.Length - encDataLength, true);
        }

        protected override byte[] GetArkEntryBytes(ArkEntry entry)
        {
            if (entry is OffsetArkEntry)
            {
                var offEntry = entry as OffsetArkEntry;

                string arkPath = ArkPath(offEntry.Part);
                byte[] data = new byte[offEntry.Size];

                using (FileStream fs = File.OpenRead(arkPath))
                {
                    fs.Seek(offEntry.PartOffset, SeekOrigin.Begin);
                    fs.Read(data, 0, data.Length);
                }

                return data; // Not very efficient or super smart at the moment
            }
            else
                // TODO: Implement reading from non-archive file on disk
                throw new Exception();
        }

        private static string[] GetPartNames(string hdrPath, int count)
        {
            string directory = Path.GetDirectoryName(hdrPath).Replace("\\", "/");
            string fileName = Path.GetFileNameWithoutExtension(hdrPath);
            string extension = Path.GetExtension(hdrPath).All(c => c == '.' || char.IsUpper(c)) ? ".ARK" : ".ark";

            string[] arkPaths = new string[count + 1];
            arkPaths[0] = hdrPath.Replace("\\", "/");

            if (!string.IsNullOrWhiteSpace(directory))
            {
                directory += "/";
            }

            for (int i = 0; i < count; i++)
            {
                arkPaths[i+1] = $"{directory}{fileName}_{i}{extension}";
            }

            return arkPaths;
        }
        
        public override void AddPendingEntry(PendingArkEntry pending)
        {
            // TODO: Check if local file path exists?
            var entry = GetArkEntry(pending.FullPath);

            if (entry == null || entry is OffsetArkEntry)
            {
                // Adds new pending entry
                _pendingEntries.Add(new PendingArkEntry(pending));
            }
            else if (entry is PendingArkEntry)
            {
                // Updates pending entry
                _pendingEntries.Remove(pending);
                _pendingEntries.Add(new PendingArkEntry(pending));
            }
        }

        protected override ArkEntry GetArkEntry(string fullPath)
        {
            var pendingEntry = _pendingEntries.FirstOrDefault(x => string.Compare(x.FullPath, fullPath, true) == 0);
            if (pendingEntry != null) return pendingEntry;

            return _offsetEntries.FirstOrDefault(x => string.Compare(x.FullPath, fullPath, true) == 0);
        }

        protected override List<ArkEntry> GetMergedEntries()
        {
            var entries = new List<ArkEntry>(_pendingEntries);
            entries.AddRange(_offsetEntries.Except<ArkEntry>(_pendingEntries));
            entries.Sort((x, y) => string.Compare(x.FullPath, y.FullPath));

            return entries;
        }
        
        public string DirectoryName => Path.GetDirectoryName(this._arkPaths[0]);
        public override string FileName => Path.GetFileName(this._arkPaths[0]);
        public override string FullPath => this._arkPaths[0];

        internal string ArkPath(int index) => this._arkPaths[index];

        /// <summary>
        /// Adds additional ark part, used for patches (experimental)
        /// </summary>
        /// <param name="path"></param>
        public void AddAdditionalPart(string path = null)
        {
            if (path is null)
            {
                // Use hdr name
                var hdrPath = _arkPaths.First();
                var fileNameNoExt = Path.GetFileNameWithoutExtension(hdrPath);
                var hdrDir = Path.GetDirectoryName(hdrPath);
                var nextPartIdx = _arkPaths.Count() - 1;

                var arkExt = (fileNameNoExt.All(c => char.IsUpper(c)))
                    ? ".ARK"
                    : ".ark";

                path = Path.Combine(hdrDir, $"{fileNameNoExt}_{nextPartIdx}{arkExt}");
            }

            var dirPath = Path.GetDirectoryName(path).Replace("\\", "/");
            var fileName = Path.GetFileName(path);
            var arkPartPath = Path.Combine(dirPath, fileName);

            // Create ark part directory
            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            // Create ark part file
            using var _ = File.Create(arkPartPath);

            // Append ark part
            _arkPaths = _arkPaths
                .Concat(new[] { arkPartPath })
                .ToArray();
        }

        public ArkFile CopyToDirectory(string dirPath)
        {
            // TODO: Apply changes during copy
            if (PendingChanges)
                throw new Exception("Can't copy archive until pending changes are applied");

            if (!Directory.Exists(dirPath))
                Directory.CreateDirectory(dirPath);

            foreach (var file in _arkPaths)
            {
                File.Copy(file, Path.Combine(dirPath, Path.GetFileName(file)), true);
            }

            return FromFile(Path.Combine(dirPath, Path.GetFileName(_arkPaths.First())));
        }

        public void SetVersion(ArkVersion version)
        {
            if (!Enum.IsDefined(typeof(ArkVersion), version))
                throw new NotSupportedException();

            _version = version;
        }
        
        public bool Encrypted { get => _encrypted; set => _encrypted = value; }
        public ArkVersion Version => this._version;

        public int PartCount() => _arkPaths.Length - 1;
    }
}
