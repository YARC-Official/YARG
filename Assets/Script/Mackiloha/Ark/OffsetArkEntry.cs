using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace Mackiloha.Ark
{
    public class OffsetArkEntry : ArkEntry
    {
        internal OffsetArkEntry(long offset, string fileName, string directory, uint size, uint inflatedSize, int part, long partOffset) : base(fileName, directory)
        {
            Part = part;
            PartOffset = partOffset;

            Offset = offset;
            Size = size;
            InflatedSize = inflatedSize;
        }
        
        public int Part { get; }
        public long PartOffset { get; }

        public long Offset { get; }
        public uint Size { get; }
        public uint InflatedSize { get; } // 0 = Already inflated
    }
}
