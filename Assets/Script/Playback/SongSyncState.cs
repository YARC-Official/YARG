namespace YARG.Playback
{
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
