namespace YARG.Playback
{
    public interface ISongSyncStateProvider
    {
        /// <summary>
        /// Advances scheduled sync state through the requested input system time before returning a snapshot.
        /// </summary>
        SongSyncState ReadSongSyncState(double inputSystemTime);
    }
}
