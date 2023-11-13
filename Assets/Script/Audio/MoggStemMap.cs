namespace YARG.Audio
{
    public readonly struct MoggStemMap
    {
        public readonly SongStem Stem;
        public readonly int[] ChannelIndicies;
        public readonly float[] Panning;

        public MoggStemMap(SongStem stem, int[] channelIndicies, float[] panning)
        {
            Stem = stem;
            ChannelIndicies = channelIndicies;
            Panning = panning;
        }

        public readonly float GetLeftPan(int index) => Panning[2 * index];
        public readonly float GetRightPan(int index) => Panning[2 * index + 1];
    } 
}