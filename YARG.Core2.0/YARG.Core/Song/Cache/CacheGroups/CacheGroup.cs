using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    public interface ICacheGroup
    {
        public int Count { get; }

        public void SerializeEntries(MemoryStream stream, Dictionary<SongEntry, CategoryCacheWriteNode> nodes);
        public bool TryRemoveEntry(SongEntry entryToRemove);

        public static void SerializeGroups<TGroup>(List<TGroup> groups, FileStream fileStream, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
            where TGroup : ICacheGroup
        {
            var streams = new MemoryStream[groups.Count];
            int length = 4;
            for (int i = 0; i < groups.Count; i++)
            {
                streams[i] = new MemoryStream();
                groups[i].SerializeEntries(streams[i], nodes);
                length += sizeof(int) + (int) streams[i].Length;
            }

            fileStream.Write(length, Endianness.Little);
            fileStream.Write(streams.Length, Endianness.Little);
            for (int i = 0; i < streams.Length; i++)
            {
                using var stream = streams[i];
                fileStream.Write((int) stream.Length, Endianness.Little);
                fileStream.Write(stream.GetBuffer(), 0, (int) stream.Length);
            }
        }
    }
}
