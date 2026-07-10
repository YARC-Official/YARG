namespace YARG.Playback
{
    public interface ISongSyncStateProvider
    {
        /// <summary>
        /// Advances scheduled sync state through the requested input system time before returning a snapshot.
        /// </summary>
        SongSyncState ReadSongSyncState(double inputSystemTime);
    }

    public readonly struct SongSyncState
    {
        public readonly float SongSpeed;
        public readonly double TargetAudioPosition;
        public readonly bool Paused;

        public SongSyncState(
            float songSpeed,
            double targetAudioPosition,
            bool paused)
        {
            SongSpeed = songSpeed;
            TargetAudioPosition = targetAudioPosition;
            Paused = paused;
        }
    }
}
