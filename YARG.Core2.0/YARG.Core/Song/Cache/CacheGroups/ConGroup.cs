using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;
using YARG.Core.IO;

namespace YARG.Core.Song.Cache
{
    public abstract class CONGroup<TEntry> : ICacheGroup
        where TEntry : RBCONEntry
    {
        protected readonly Dictionary<string, SortedDictionary<int, TEntry>> entries = new();

        public readonly string Location;
        public readonly AbridgedFileInfo Info;
        public readonly string DefaultPlaylist;
        public readonly FixedArray<byte> SongDTAData;

        public int Count
        {
            get
            {
                int count = 0;
                foreach (var node in entries.Values)
                {
                    count += node.Count;
                }
                return count;
            }
        }

        protected CONGroup(in FixedArray<byte> songDTAData, string location, in AbridgedFileInfo info, string defaultPlaylist)
        {
            SongDTAData = songDTAData;
            Location = location;
            Info = info;
            DefaultPlaylist = defaultPlaylist;
        }

        public abstract void ReadEntry(string nodeName, int index, RBProUpgrade upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings);

        public void SerializeEntries(MemoryStream groupStream, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            groupStream.Write(Location);
            groupStream.Write(Info.LastUpdatedTime.ToBinary(), Endianness.Little);
            groupStream.Write(Count, Endianness.Little);

            using var entryStream = new MemoryStream();
            foreach (var entryList in entries)
            {
                foreach (var entry in entryList.Value)
                {
                    groupStream.Write(entryList.Key);
                    groupStream.Write(entry.Key, Endianness.Little);

                    entryStream.SetLength(0);
                    entry.Value.Serialize(entryStream, nodes[entry.Value]);

                    groupStream.Write((int) entryStream.Length, Endianness.Little);
                    groupStream.Write(entryStream.GetBuffer(), 0, (int) entryStream.Length);
                }
            }
        }

        public void AddEntry(string name, int index, TEntry entry)
        {
            SortedDictionary<int, TEntry> dict;
            lock (entries)
            {
                if (!entries.TryGetValue(name, out dict))
                {
                    entries.Add(name, dict = new SortedDictionary<int, TEntry>());
                }
            }

            lock (dict)
            {
                dict.Add(index, entry);
            }
        }

        public bool RemoveEntries(string name)
        {
            lock (entries)
            {
                if (!entries.Remove(name, out var dict))
                    return false;
            }
            return true;
        }

        public void RemoveEntry(string name, int index)
        {
            lock (entries)
            {
                var dict = entries[name];
                dict.Remove(index);
                if (dict.Count == 0)
                {
                    entries.Remove(name);
                }
            }
        }

        public bool TryGetEntry(string name, int index, out TEntry? entry)
        {
            entry = null;
            lock (entries)
            {
                return entries.TryGetValue(name, out var dict) && dict.TryGetValue(index, out entry);
            }
        }

        public bool TryRemoveEntry(SongEntry entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            foreach (var dict in entries)
            {
                foreach (var entry in dict.Value)
                {
                    if (ReferenceEquals(entry.Value, entryToRemove))
                    {
                        dict.Value.Remove(entry.Key);
                        if (dict.Value.Count == 0)
                        {
                            entries.Remove(dict.Key);
                        }
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
