using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.Milo2
{
    public class MiloFile
    {
        private const uint MAX_BLOCK_SIZE = 0x20000;
        private const uint ADDE_PADDING = 0xADDEADDE;
        private static readonly byte[] ADDE_PADDING_BYTES = { 0xAD, 0xDE, 0xAD, 0xDE };

        private BlockStructure _structure;
        private uint _offset;
        
        public MiloFile()
        {
            _structure = BlockStructure.MILO_B;
            _offset = 2064;
        }
        
        public static MiloFile ReadFromFile(string path)
        {
            using (var fs = File.OpenRead(path))
            {
                return ReadFromStream(new AwesomeReader(fs, false));
            }
        }

        public static MiloFile ReadFromStream(Stream stream)
        {
            return ReadFromStream(new AwesomeReader(stream, false));
        }

        public static MiloFile ReadFromStream(AwesomeReader ar)
        {
            long startingOffset = ar.BaseStream.Position; // You might not be starting at 0x0
            var structureType = (BlockStructure)ar.ReadUInt32();

            //if (structureType != BlockStructure.MILO_A)
            //    throw new Exception("Unsupported milo compression");

            uint offset = ar.ReadUInt32(); // Start of blocks
            int blockCount = ar.ReadInt32();
            int maxBlockSize = ar.ReadInt32(); // Skips max uncompressed size

            // Reads block sizes
            var sizes = Enumerable.Range(0, blockCount).Select(x => ar.ReadUInt32()).ToArray();

            // Jumps to first block offset
            ar.BaseStream.Position = startingOffset + offset;
            
            using (var ms = new MemoryStream())
            {
                foreach (var size in sizes)
                {
                    bool compressed = (structureType == BlockStructure.MILO_D && (size & 0xFF000000) == 0)
                                    || structureType == BlockStructure.MILO_B
                                    || structureType == BlockStructure.MILO_C;

                    uint blockSize = size & 0x00FFFFFF;

                    byte[] block = ar.ReadBytes((int)blockSize);

                    if (block.Length > 0 && compressed)
                    {
                        switch (structureType)
                        {
                            case BlockStructure.MILO_B:
                                block = Compression.InflateBlock(block, CompressionType.ZLIB);
                                break;
                            case BlockStructure.MILO_C:
                                block = Compression.InflateBlock(block, CompressionType.GZIP);
                                break;
                            default: // MILO_D
                                block = Compression.InflateBlock(block, CompressionType.ZLIB, 4);
                                break;
                        }
                    }

                    ms.Write(block, 0, block.Length);
                }
                
                ms.Seek(0, SeekOrigin.Begin);
                
                return new MiloFile()
                {
                    _structure = structureType,
                    _offset = offset,
                    Data = ms.ToArray()
                };
            }
        }

        public void WriteToFile(string path)
        {
            using (var fs = File.Create(path))
            {
                WriteToStream(new AwesomeWriter(fs, false));
            }
        }

        public void WriteToStream(Stream stream)
        {
            WriteToStream(new AwesomeWriter(stream, false));
        }

        public void WriteToStream(AwesomeWriter aw)
        {
            long startOffset = aw.BaseStream.Position;

            // Writes just raw data if no block structure
            switch (_structure)
            {
                case BlockStructure.NONE:
                    aw.Write(Data);
                    return;
                case BlockStructure.GZIP:
                    aw.Write(Compression.DeflateBlock(Data, CompressionType.GZIP));
                    return;
            }

            // Milo block structure
            using (var ar = new AwesomeReader(new MemoryStream(Data)))
            {
                List<int> blockSizes = new List<int>();
                int currentBlockSize = 0, largetBlockSize = 0;

                // Calculates uncompressed block sizes
                while (ar.BaseStream.Position < ar.BaseStream.Length)
                {
                    // This assumes at least one embedded file entry - Fix later?
                    long nextAdde = ar.FindNext(ADDE_PADDING_BYTES);
                    ar.BaseStream.Position += 4;
                    currentBlockSize += (int)nextAdde + 4;

                    if (currentBlockSize >= MAX_BLOCK_SIZE || ar.BaseStream.Position >= ar.BaseStream.Length)
                    {
                        if (currentBlockSize > largetBlockSize) largetBlockSize = currentBlockSize; // Sets larget block size

                        blockSizes.Add(currentBlockSize);
                        currentBlockSize = 0;
                    }
                }

                // Writes header (16 bytes)
                aw.Write((int)_structure);
                aw.Write(_offset);
                aw.Write(blockSizes.Count);
                aw.Write(largetBlockSize);
                aw.BaseStream.Seek(startOffset + _offset, SeekOrigin.Begin);

                ar.BaseStream.Seek(0, SeekOrigin.Begin);

                // Compresses blocks
                for (int i = 0; i < blockSizes.Count; i++)
                {
                    byte[] block = ar.ReadBytes(blockSizes[i]);

                    switch (_structure)
                    {
                        case BlockStructure.MILO_B:
                            block = Compression.DeflateBlock(block, CompressionType.ZLIB);
                            break;
                        case BlockStructure.MILO_C: // Gzip
                            block = Compression.DeflateBlock(block, CompressionType.GZIP);
                            break;
                        case BlockStructure.MILO_D:
                            byte[] size = BitConverter.GetBytes(block.Length);
                            byte[] temp = Compression.DeflateBlock(block, CompressionType.ZLIB);

                            block = new byte[temp.Length + 4];
                            Array.Copy(size, 0, block, 0, size.Length);
                            Array.Copy(temp, 0, block, size.Length, block.Length - size.Length);
                            break;
                    }

                    // Updates block size
                    blockSizes[i] = block.Length;

                    // Writes block
                    aw.Write(block);
                }
                
                // Writes block sizes in header
                aw.BaseStream.Seek(startOffset + 16, SeekOrigin.Begin);
                blockSizes.ForEach(blockSize => aw.Write(blockSize));
            }
        }

        private (int Version, bool BigEndian) GuessVersion()
        {
            if (Data == null || Data.Length < 4
                || Data[1] != 0 || Data[2] != 0)
                return (-1, false);

            // Version should be between 6-32 (should be)
            return (Data[3] != 0) ? ((int)Data[3], true) : ((int)Data[0], false);

        }

        //public MiloObjectDir Directory { get; set; }
        public BlockStructure Structure { get => _structure; set => _structure = value; }
        public byte[] Data { get; set; }

        public bool BigEndian => GuessVersion().BigEndian;
        public int Version => GuessVersion().Version;

        /*
        private static string[] GetExternalResources(AwesomeReader ar)
        {
            string[] res = new string[ar.ReadUInt32()];

            // Mostly zero'd
            for (int i = 0; i < res.Length; i++)
            {
                uint charCount = ar.ReadUInt32();

                // Reads string if not some outrageous number
                if (charCount < 0xFFFF)
                {
                    ar.BaseStream.Position -= 4;
                    res[i] = ar.ReadString();
                }
            }

            return res;
        }

        private static MiloFile ParseDirectory(AwesomeReader ar)
        {
            // Guesses endianess
            ar.BigEndian = DetermineEndianess(ar.ReadBytes(4), out MiloVersion version, out bool valid);
            if (!valid)
                throw new Exception($"Milo directory version of {version} is not supported");

            string dirType = "", dirName = "";
            byte[] dirData = null;

            if ((int)version >= 24)
            {
                dirType = ar.ReadString();
                dirName = ar.ReadString();
                ar.BaseStream.Position += 8; // Skips string count + total length
            }

            // Reads entry types/names
            int count = ar.ReadInt32();
            string[] types = new string[count];
            string[] names = new string[count];

            for (int i = 0; i < count; i++)
            {
                types[i] = ar.ReadString();
                names[i] = ar.ReadString();
            }

            if (version == MiloVersion.V10)
            {
                // Just reads over for now
                GetExternalResources(ar);
            }
            else
            {
                // Skips unknown data
                var next = FindNext(ar, ADDE_PADDING);
                dirData = ar.ReadBytes(next);
                ar.BaseStream.Seek(4, SeekOrigin.Current);
            }

            MiloFile milo = new MiloFile()
            {
                _version = (MiloVersion)version,
                _directoryEntry = new MiloEntry(dirName, dirType, dirData),
                _bigEndian = ar.BigEndian
            };
            
            // Reads each file
            for (int i = 0; i < names.Length; i++)
            {
                long start = ar.BaseStream.Position;
                int size = FindNext(ar, ADDE_PADDING);
                byte[] data = ar.ReadBytes(size);
                ar.BaseStream.Position += 4;

                milo.Entries.Add(new MiloEntry(names[i], types[i], data));
            }

            return milo;
        }

        public void WriteToFile(string path)
        {
            using (var fs = File.OpenWrite(path))
            {
                WriteBlockedMiloToStream(fs);
            }
        }

        public void WriteBlockedMiloToStream(Stream stream)
        {
            using (var ms = new MemoryStream())
            {
                WriteMiloToStream(new AwesomeWriter(ms, _bigEndian), out List<int> blockSizes);
                ms.Seek(0, SeekOrigin.Begin);
                //ms.CopyTo(stream);

                if (_structure == BlockStructure.NONE || _structure == BlockStructure.GZIP)
                {
                    // TODO: Implement GZIP compression
                    ms.CopyTo(stream);
                    return;
                }

                // Classic milo block structure
                using (var aw = new AwesomeWriter(stream, false))
                {
                    // Calculates offset for start of block data, defaults to max 512 blocks if a larger offset is needed
                    var maxBlocks = (_offset - 16) / 4;
                    var offset = (maxBlocks < blockSizes.Count) ? Math.Max(512, blockSizes.Count) : (int)maxBlocks;
                    offset = (offset * 4) + 16;
                    
                    aw.Write((int)_structure);
                    aw.Write(offset);
                    aw.Write(blockSizes.Count);
                    aw.Write(blockSizes.Max());
                    
                    if (_structure == BlockStructure.MILO_A)
                    {
                        // No compression, just write the whole thing and be done with it
                        blockSizes.ForEach(x => aw.Write(x));
                        aw.BaseStream.Seek(offset, SeekOrigin.Begin);
                        ms.CopyTo(aw.BaseStream);
                        return;
                    }

                    var comType = (_structure == BlockStructure.MILO_C) ? CompressionType.GZIP : CompressionType.ZLIB;
                    var writeSize = _structure == BlockStructure.MILO_D;
                    aw.BaseStream.Seek(offset, SeekOrigin.Begin);

                    for (int i = 0; i < blockSizes.Count; i++)
                    {
                        // Compresses block
                        byte[] data = new byte[blockSizes[i]];
                        ms.Read(data, 0, data.Length);
                        data = Compression.DeflateBlock(data, comType);

                        // Writes block (+ uncompressed size if required)
                        if (writeSize)
                        {
                            aw.Write(blockSizes[i]);
                            aw.Write(data);
                            blockSizes[i] = data.Length + 4;
                        }
                        else
                        {
                            aw.Write(data);
                            blockSizes[i] = data.Length;
                        }
                    }

                    // Writes block sizes
                    aw.BaseStream.Seek(16, SeekOrigin.Begin);
                    blockSizes.ForEach(x => aw.Write(x));
                }
            }
        }

        private void WriteMiloToStream(AwesomeWriter aw, out List<int> blockSizes)
        {
            var prevOffset = aw.BaseStream.Position;
            blockSizes = new List<int>();
            WriteDirectoryInfo(aw);

            foreach (var ientry in Entries)
            {
                if (!(ientry is MiloEntry)) continue; // TODO: Handle directory entries

                var currentSize = aw.BaseStream.Position - prevOffset;
                if (currentSize > MAX_BLOCK_SIZE)
                {
                    blockSizes.Add((int)currentSize);
                    prevOffset = aw.BaseStream.Position;
                }

                var entry = ientry as MiloEntry;

                aw.Write(entry.Data);
                aw.Write(ADDE_PADDING_BYTES);
            }

            // Adds last block size
            blockSizes.Add((int)(aw.BaseStream.Position - prevOffset));
        }

        private void WriteDirectoryInfo(AwesomeWriter aw)
        {
            var version = (int)_version;

            aw.Write(version);

            if (version >= 24)
            {
                aw.Write(_directoryEntry?.Type ?? "");
                aw.Write(_directoryEntry?.Name ?? "");

                // Name/type count for entries and directory
                aw.Write((Entries.Count * 2) + 2);

                // Name count and lengths for entries and directory
                aw.Write(1 + (_directoryEntry?.Name ?? "").Length + Entries.Count + Entries.Select(x => (x.Name ?? "").Length).Sum());
            }

            aw.Write(Entries.Count);

            foreach (var entry in Entries)
            {
                aw.Write(entry.Type);
                aw.Write(entry.Name);
            }

            if (version == 10)
            {
                // TODO: Parse textures to get external paths
                var externalResources = Entries.Where(x => x.Type.Equals("Tex", StringComparison.CurrentCultureIgnoreCase)).ToArray();

                aw.Write(externalResources.Length);
                aw.BaseStream.Position += externalResources.Length * sizeof(int);
            }
            else if (version >= 24)
            {
                if (_directoryEntry?.Data != null)
                    aw.Write(_directoryEntry.Data);

                aw.Write(ADDE_PADDING_BYTES);
            }
        }

        private static bool DetermineEndianess(byte[] head, out MiloVersion version, out bool valid)
        {
            bool IsVersionValid(MiloVersion v) => Enum.IsDefined(v.GetType(), v);

            bool bigEndian = false;
            version = (MiloVersion)BitConverter.ToInt32(head, 0);
            valid = IsVersionValid(version);

            checkVersion:
            if (!valid && !bigEndian)
            {
                bigEndian = !bigEndian;
                Array.Reverse(head);
                version = (MiloVersion)BitConverter.ToInt32(head, 0);
                valid = IsVersionValid(version);

                goto checkVersion;
            }

            return bigEndian;
        }
        
        private static int FindNext(AwesomeReader ar, uint magic)
        {
            long start = ar.BaseStream.Position, currentPosition = ar.BaseStream.Position;
            uint currentMagic = 0;

            while (magic != currentMagic)
            {
                if (ar.BaseStream.Position == ar.BaseStream.Length)
                {
                    // Couldn't find it
                    ar.BaseStream.Seek(start, SeekOrigin.Begin);
                    return -1;
                }

                currentMagic = (uint)((currentMagic << 8) | ar.ReadByte());
                currentPosition++;
            }

            ar.BaseStream.Seek(start, SeekOrigin.Begin);
            return (int)((currentPosition - 4) - start);
        }
        

        public MiloVersion Version => _version;
        public int Size => _size;
        public MiloEntry DirectoryEntry => _directoryEntry;

        public List<IMiloEntry> Entries { get; }
        */
    }
}
