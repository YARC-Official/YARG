namespace YARG.Playback
{
    public readonly struct SongSyncState
    {
        public readonly float SongSpeed;
        public readonly double SongOffset;
        public readonly double AudioCalibration;
        public readonly double InputTimeOffset;
        public readonly bool Paused;

        public SongSyncState(
            float songSpeed,
            double songOffset,
            double audioCalibration,
            double inputTimeOffset,
            bool paused)
        {
            SongSpeed = songSpeed;
            SongOffset = songOffset;
            AudioCalibration = audioCalibration;
            InputTimeOffset = inputTimeOffset;
            Paused = paused;
        }
    }
}
