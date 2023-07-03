namespace YARG.Gameplay
{
    public struct Beat
    {
        public uint Position;
        public float Time;
        public BeatStyle Style;

        public Beat(uint position, float time, BeatStyle type)
        {
            Position = position;
            Time = time;
            Style = type;
        }
    }

    public enum BeatStyle
    {
        Measure,
        Strong,
        Weak,
    }
}