namespace YARG.Core.Chart
{
    /// <summary>
    /// A general event that occurs in a chart: notes, phrases, text events, etc.
    /// </summary>
    public abstract class ChartEvent
    {
        public double Time       { get; set; }
        public double TimeLength { get; set; }
        public double TimeEnd    => Time + TimeLength;

        public uint Tick       { get; set; }
        public uint TickLength { get; set; }
        public uint TickEnd    => Tick + TickLength;

        // For subclasses that set the base properties through other parameters
        public ChartEvent() {}

        public ChartEvent(double time, double timeLength, uint tick, uint tickLength)
        {
            Time = time;
            TimeLength = timeLength;
            Tick = tick;
            TickLength = tickLength;
        }

        public ChartEvent(ChartEvent other)
            : this(other.Time, other.TimeLength, other.Tick, other.TickLength)
        {
        }

        protected bool Equals(ChartEvent other)
        {
            return Time == other.Time &&
                TimeLength == other.TimeLength &&
                Tick == other.Tick &&
                TickLength == other.TickLength;
        }
    }
}