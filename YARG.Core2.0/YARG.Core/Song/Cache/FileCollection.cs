using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Song.Cache
{
    public readonly struct FileCollection
    {
        public readonly DirectoryInfo Directory;
        public readonly Dictionary<string, FileInfo> Subfiles;
        public readonly Dictionary<string, DirectoryInfo> SubDirectories;
        public readonly bool ContainedDupes;

        internal static bool TryCollect(string directory, out FileCollection collection)
        {
            var info = new DirectoryInfo(directory);
            if (!info.Exists)
            {
                collection = default;
                return false;
            }
            collection = new FileCollection(info);
            return true;
        }

        internal FileCollection(DirectoryInfo directory)
        {
            Directory = directory;
            Subfiles = new();
            SubDirectories = new();
            var dupedFiles = new HashSet<string>();
            var dupedDirectories = new HashSet<string>();

            foreach (var info in directory.EnumerateFileSystemInfos())
            {
                string name = info.Name.ToLowerInvariant();
                switch (info)
                {
                    case FileInfo subFile:
                        if (!Subfiles.TryAdd(name, subFile))
                        {
                            dupedFiles.Add(name);
                        }
                        break;
                    case DirectoryInfo subDirectory:
                        if (!SubDirectories.TryAdd(name, subDirectory))
                        {
                            dupedDirectories.Add(name);
                        }
                        break;
                }
            }

            // Removes any sort of ambiguity from duplicates
            ContainedDupes = dupedFiles.Count > 0 || dupedDirectories.Count > 0;
            foreach (var dupe in dupedFiles)
            {
                Subfiles.Remove(dupe);
            }

            foreach (var dupe in dupedDirectories)
            {
                SubDirectories.Remove(dupe);
            }
        }

        public bool ContainsAudio()
        {
            foreach (var subFile in Subfiles.Keys)
            {
                if (IniAudio.IsAudioFile(subFile))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
