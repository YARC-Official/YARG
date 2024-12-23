using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public sealed class UnpackedCONGroup : CONGroup<UnpackedRBCONEntry>
    {
        public readonly Dictionary<string, List<YARGTextContainer<byte>>> SongNodes = new();

        public UnpackedCONGroup(in FixedArray<byte> data, Dictionary<string, List<YARGTextContainer<byte>>> songNodes, string directory, in AbridgedFileInfo dta, string defaultPlaylist)
            : base(in data, directory, in dta, defaultPlaylist)
        {
            SongNodes = songNodes;
        }

        public override void ReadEntry(string nodeName, int index, RBProUpgrade upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var song = UnpackedRBCONEntry.TryLoadFromCache(Location, in Info, nodeName, upgrade, stream, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }
    }
}
