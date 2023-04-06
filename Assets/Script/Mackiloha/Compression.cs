using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression; // For GZip/ZLib compression

namespace Mackiloha
{
    public enum CompressionType
    {
        ZLIB,
        GZIP
    }

    public static class Compression
    {
        private static readonly byte[] ZLIB_MAGIC = { 0x78, 0x9C }; // Default compression

        public static byte[] InflateBlock(byte[] inBlock, CompressionType type, int offset = 0)
        {
            if (offset < 0) offset = 0;
            byte[] outBlock;
            const int MAX_READ_SIZE = 0x8000;

            switch(type)
            {
                case CompressionType.GZIP:
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Decompresses gzip stream
                        GZipStream gzip = new GZipStream(new MemoryStream(inBlock.Skip(offset).ToArray()), CompressionMode.Decompress);

                        gzip.CopyTo(ms);
                        outBlock = ms.ToArray();
                        gzip.Flush();
                    }
                    break;
                case CompressionType.ZLIB:
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Decompresses zlib stream
                        using (var outZStream = new DeflateStream(new MemoryStream(inBlock.Skip(offset).ToArray()), CompressionMode.Decompress, true))
                        {
                            outZStream.CopyTo(ms);
                        }

                        outBlock = ms.ToArray();
                    }
                    break;
                default:
                    outBlock = new byte[inBlock.Length];
                    Array.Copy(inBlock, outBlock, inBlock.Length);
                    break;
            }

            return outBlock;
        }

        public static byte[] DeflateBlock(byte[] inBlock, CompressionType type, int offset = 0)
        {
            if (offset < 0) offset = 0;
            byte[] outBlock;

            switch (type)
            {
                case CompressionType.GZIP:
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Compresses gzip stream
                        GZipStream gzip = new GZipStream(new MemoryStream(inBlock.Skip(offset).ToArray()), CompressionMode.Compress);

                        gzip.CopyTo(ms);
                        outBlock = ms.ToArray();
                        gzip.Flush();
                    }
                    break;
                case CompressionType.ZLIB:
                    using (MemoryStream ms = new MemoryStream())
                    {
                        // Compresses zlib stream
                        using (var outZStream = new DeflateStream(ms, CompressionLevel.Optimal, true))
                        {
                            outZStream.Write(inBlock, offset, inBlock.Length - offset);
                        }
                        
                        outBlock = ms.ToArray();
                    }
                    return outBlock; // Returns without magic
                default:
                    outBlock = new byte[inBlock.Length];
                    Array.Copy(inBlock, outBlock, inBlock.Length);
                    break;
            }

            return outBlock;
        }
    }
}
