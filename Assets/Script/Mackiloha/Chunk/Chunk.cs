using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

/*
All bytes are in big endian order.

It looks like milo files were replaced with this. Max Block Size = 0x10000 (2^16)

BYTES(4) - "CHNK"
INT32 - Uknown - Always 255?
INT32 - Block Count
INT32 - Largest Block (Uncompressed)
INT16 - Always 1
INT16 - Always 2
BlockDetails[Block Count]

* ----Block Details----
* =====================
* INT32 - Size
* INT32 - Decompressed Size
* Bool? - If "01 00 00 00", then it's compressed.
* INT32 - Offset

Begin ZLib'd blocks!
*/

namespace Mackiloha.Chunk
{
    // Successor to Milo container (Used in FME/RBVR)
    public class Chunk
    {
        private const uint CHNK_MAGIC = 0x43484E4B; // "CHNK"

        public Chunk()
        {
            Entries = new List<ChunkEntry>();
        }

        public void WriteToFile(string outPath, bool noHeader = false)
        {
            using (FileStream fs = File.OpenWrite(outPath))
            {
                WriteToStream(fs, noHeader);
            }
        }

        public void WriteToStream(Stream stream, bool noHeader)
        {
            AwesomeWriter aw = new AwesomeWriter(stream, true);

            if (!noHeader)
            {
                aw.Write(CHNK_MAGIC);
                aw.Write((int)255);
                aw.Write(Entries.Count);
                aw.Write(Entries.Max(x => x.Data.Length));
                aw.Write((short)1);
                aw.Write((short)2);

                int currentIdx = 20 + (Entries.Count << 2);

                // Writes block details
                foreach (ChunkEntry entry in Entries)
                {
                    aw.Write(entry.Data.Length);
                    aw.Write(entry.Data.Length);

                    aw.Write((int)(entry.Compressed ? 1 : 0));
                    aw.Write(currentIdx);

                    currentIdx += entry.Data.Length;
                }
            }
            
            // Writes blocks
            Entries.ForEach(x => aw.Write(x.Data));
        }

        public static void DecompressChunkFile(string inPath, string outPath, bool noHeader)
        {
            using (FileStream fs = File.OpenRead(inPath))
            {
                Chunk chunk = ReadFromStream(fs);
                chunk.WriteToFile(outPath, noHeader);
            }
        }

        private static Chunk ReadFromStream(Stream stream)
        {
            Chunk chunk = new Chunk();
            AwesomeReader ar = new AwesomeReader(stream, true);

            if (ar.ReadUInt32() != CHNK_MAGIC) return chunk;

            ar.BaseStream.Position += 4; // Always 255?
            int blockCount = ar.ReadInt32();
            ar.BaseStream.Position += 8; // Skips 1, 2 (16-bits)

            int[] blockSize = new int[blockCount];
            bool[] compressed = new bool[blockCount]; // Uncompressed by default

            // Reads block details
            for (int i = 0; i < blockCount; i++)
            {
                blockSize[i] = ar.ReadInt32();
                ar.BaseStream.Position += 4; // Decompressed size (Not needed)

                // Sets as compressed if it meets the requirement
                compressed[i] = ar.ReadInt32() == 0x1000000;

                ar.BaseStream.Position += 4; // Offset (Not needed)
            }
            
            for (int i = 0; i < blockCount; i++)
            {
                // Reads block bytes
                byte[] block = ar.ReadBytes(blockSize[i]);

                // Decompresses if needed
                if (block.Length > 0 && compressed[i])
                {
                    block = Compression.InflateBlock(block, CompressionType.ZLIB);
                    blockSize[i] = block.Length;
                }

                chunk.Entries.Add(new ChunkEntry()
                {
                    Data = block,
                    Compressed = false
                });

                // TODO: Write blocks to stream, and parse internal file system instead
                // ms.Write(block, 0, block.Length);
            }

            return chunk;
        }
        
        public List<ChunkEntry> Entries;
    }

    public class ChunkEntry
    {
        public byte[] Data;
        public bool Compressed;
    }
}
