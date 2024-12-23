using System.Collections.Generic;
using System.IO;
using YARG.Core.Extensions;

namespace YARG.Core.Song.Cache
{
    public sealed class IniGroup : ICacheGroup
    {
        public readonly string Directory;
        public readonly Dictionary<HashWrapper, List<IniSubEntry>> entries = new();

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

        public IniGroup(string directory)
        {
            Directory = directory;
        }

        public void AddEntry(IniSubEntry entry)
        {
            var hash = entry.Hash;
            List<IniSubEntry> list;
            lock (entries)
            {
                if (!entries.TryGetValue(hash, out list))
                {
                    entries.Add(hash, list = new List<IniSubEntry>());
                }
            }

            lock (list)
            {
                list.Add(entry);
            }
        }

        public bool TryRemoveEntry(SongEntry entryToRemove)
        {
            // No locking as the post-scan removal sequence
            // cannot be parallelized
            if (entries.TryGetValue(entryToRemove.Hash, out var list))
            {
                if (list.RemoveAll(entry => ReferenceEquals(entry, entryToRemove)) > 0)
                {
                    if (list.Count == 0)
                    {
                        entries.Remove(entryToRemove.Hash);
                    }
                    return true;
                }
            }
            return false;
        }

        public void SerializeEntries(MemoryStream groupStream, Dictionary<SongEntry, CategoryCacheWriteNode> nodes)
        {
            groupStream.Write(Directory);
            groupStream.Write(Count, Endianness.Little);

            using MemoryStream entryStream = new();
            foreach (var shared in entries)
            {
                foreach (var entry in shared.Value)
                {
                    entryStream.SetLength(0);

                    // Validation block
                    entryStream.Write(entry.SubType == EntryType.Sng);
                    string relativePath = Path.GetRelativePath(Directory, entry.Location);
                    if (relativePath == ".")
                    {
                        relativePath = string.Empty;
                    }
                    entryStream.Write(relativePath);
                    entry.Serialize(entryStream, nodes[entry]);

                    groupStream.Write((int) entryStream.Length, Endianness.Little);
                    groupStream.Write(entryStream.GetBuffer(), 0, (int) entryStream.Length);
                }
            }
        }
    }
}
