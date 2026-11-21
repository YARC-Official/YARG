using YARG.Core.Song;

namespace YARG.Integration
{
    internal struct SetlistSnapshotEntry
    {
        public HashWrapper Hash;
        public uint LengthMs;
        public string Title;
        public string Artist;
        public string Album;
    }

    /// <summary>
    /// A copy of the setlist that the main thread hands to the socket thread. It holds no <see cref="SongEntry"/>,
    /// so the socket thread never reads library objects.
    /// </summary>
    internal sealed class SetlistSnapshot
    {
        public uint Revision;
        public bool Active;
        public bool ShowInProgress;
        public int CurrentIndex;

        /// <summary>
        /// Songs in the setlist, including any beyond <see cref="EntryCount"/>.
        /// </summary>
        public int TotalCount;

        public int EntryCount;
        public readonly SetlistSnapshotEntry[] Entries = new SetlistSnapshotEntry[SetlistProtocol.MAX_SNAPSHOT_ENTRIES];
    }
}
