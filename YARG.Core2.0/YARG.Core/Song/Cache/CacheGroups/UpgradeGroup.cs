using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public sealed class UpgradeGroup : IModificationGroup
    {
        public readonly string Directory;
        public readonly DateTime DTALastWrite;
        public readonly Dictionary<string, (YARGTextContainer<byte> Container, UnpackedRBProUpgrade Upgrade)> Upgrades;
        public readonly FixedArray<byte> DTAData;

        public UpgradeGroup(string directory, DateTime lastWrite, in FixedArray<byte> data, Dictionary<string, (YARGTextContainer<byte> Node, UnpackedRBProUpgrade Upgrade)> upgrades)
        {
            Directory = directory;
            DTALastWrite = lastWrite;
            Upgrades = upgrades;
            DTAData = data;
        }

        public void SerializeModifications(MemoryStream stream)
        {
            stream.Write(Directory);
            stream.Write(DTALastWrite.ToBinary(), Endianness.Little);
            stream.Write(Upgrades.Count, Endianness.Little);
            foreach (var node in Upgrades)
            {
                stream.Write(node.Key);
                node.Value.Upgrade.WriteToCache(stream);
            }
        }
    }
}
