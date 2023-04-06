using System;
using System.Collections.Generic;
using System.Collections.ObjectModel; // ReadOnlyCollection
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Mackiloha.Ark
{
    public abstract class Archive
    {
        protected readonly List<PendingArkEntry> _pendingEntries;
        protected string _workingDirectory;

        public Stream GetArkEntryFileStream(ArkEntry entry) => new MemoryStream(GetArkEntryBytes(entry), false); // Read-only
        protected abstract byte[] GetArkEntryBytes(ArkEntry entry);

        protected Archive()
        {
            _pendingEntries = new List<PendingArkEntry>();
        }

        public void SetWorkingDirectory(string path)
        {
            path = Path.GetFullPath(path).Replace("\\", "/");

            // Checks access permission
            if (!FileHelper.HasAccess(path))
            {
                // Do something here
            }

            this._workingDirectory = path;
        }

        public abstract void CommitChanges();

        protected abstract ArkEntry GetArkEntry(string fullPath);

        protected abstract List<ArkEntry> GetMergedEntries();

        public abstract void AddPendingEntry(PendingArkEntry pending);

        public ArkEntry this[string fullPath] => GetArkEntry(fullPath);
        public ReadOnlyCollection<ArkEntry> Entries => new ReadOnlyCollection<ArkEntry>(GetMergedEntries());
        public bool PendingChanges => _pendingEntries.Count > 0;
        public string WorkingDirectory => this._workingDirectory;

        public abstract string FileName { get; }
        public abstract string FullPath { get; }
    }
}
