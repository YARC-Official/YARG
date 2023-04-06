using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.Ark
{
    public class ArkFileSystem : Archive
    {
        private string _fullPath;
        private readonly List<ArkEntry> _entries;

        private ArkFileSystem() : base()
        {
            _entries = new List<ArkEntry>();
        }

        public static ArkFileSystem FromDirectory(string input)
        {
            if (!Directory.Exists(input)) throw new DirectoryNotFoundException();

            ArkFileSystem ark = new ArkFileSystem();
            string[] files = Directory.GetFiles(input, "*", SearchOption.AllDirectories);
            input = input.Replace("\\", "/");

            foreach (var file in files)
            {
                var relativePath = file.Replace("\\", "/").Replace(input, string.Empty).Remove(0, 1);

                int lastIdx = relativePath.LastIndexOf('/');
                string filePath = (lastIdx < 0) ? relativePath : relativePath.Remove(0, lastIdx + 1);
                string direPath = (lastIdx < 0) ? "" : relativePath.Substring(0, lastIdx);

                ark._entries.Add(new ArkEntry(filePath, direPath));
            }

            // Sets archive path
            ark._fullPath = input;

            return ark;
        }

        public override void CommitChanges()
        {
            throw new NotImplementedException();
        }

        protected override byte[] GetArkEntryBytes(ArkEntry entry)
        {
            string filePath = Path.Combine(_fullPath, entry.FullPath);
            var bytes = File.ReadAllBytes(filePath);

            // TODO: Figure out a better place to put this compression check
            if (entry.Extension.Equals("gz", StringComparison.CurrentCultureIgnoreCase))
            {
                return Compression.InflateBlock(bytes, CompressionType.GZIP);
            }
            // TODO: Check zlib (Else if statement)

            return bytes;
        }

        public override void AddPendingEntry(PendingArkEntry pending)
        {
            throw new NotImplementedException();
        }

        protected override ArkEntry GetArkEntry(string fullPath)
        {
            var pendingEntry = _pendingEntries.FirstOrDefault(x => string.Compare(x.FullPath, fullPath, true) == 0);
            if (pendingEntry != null) return pendingEntry;

            return _entries.FirstOrDefault(x => string.Compare(x.FullPath, fullPath, true) == 0);
        }

        protected override List<ArkEntry> GetMergedEntries()
        {
            var entries = new List<ArkEntry>(_pendingEntries);
            entries.AddRange(_entries.Except<ArkEntry>(_pendingEntries));
            entries.Sort((x, y) => string.Compare(x.FullPath, y.FullPath));

            return entries;
        }

        public override string FileName => Path.GetFileName(_fullPath);
        public override string FullPath => _fullPath;
    }
}
