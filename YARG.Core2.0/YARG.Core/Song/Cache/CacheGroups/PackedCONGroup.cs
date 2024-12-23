using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class PackedCONGroup : CONGroup<PackedRBCONEntry>, IModificationGroup
    {
        public readonly List<CONFileListing> Listings;
        public readonly Dictionary<string, List<YARGTextContainer<byte>>> SongNodes = new();
        public readonly Dictionary<string, (YARGTextContainer<byte> Container, PackedRBProUpgrade Upgrade)> Upgrades = new();
        public readonly FixedArray<byte> UpgradeDTAData;
        public FileStream Stream;

        public PackedCONGroup(List<CONFileListing> listings, FixedArray<byte> songData, FixedArray<byte> upgradeData,
            Dictionary<string, List<YARGTextContainer<byte>>> songNodes, Dictionary<string, (YARGTextContainer<byte>, PackedRBProUpgrade)> upgrades,
            in AbridgedFileInfo info, string defaultPlaylist)
            : base(in songData, info.FullName, in info, defaultPlaylist)
        {
            Listings = listings;
            SongNodes = songNodes;
            Upgrades = upgrades;
            UpgradeDTAData = upgradeData;
            Stream = null!;
        }

        public override void ReadEntry(string nodeName, int index, RBProUpgrade upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var song = PackedRBCONEntry.TryLoadFromCache(Listings, nodeName, upgrade, stream, strings);
            if (song != null)
            {
                AddEntry(nodeName, index, song);
            }
        }

        public void SerializeModifications(MemoryStream stream)
        {
            stream.Write(Location);
            stream.Write(Info.LastUpdatedTime.ToBinary(), Endianness.Little);
            stream.Write(Upgrades.Count, Endianness.Little);
            foreach (var node in Upgrades)
            {
                stream.Write(node.Key);
                node.Value.Upgrade.WriteToCache(stream);
            }
        }
    }
}
