using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A performer on the venue's stage.
    /// </summary>
    [Flags]
    public enum Performer
    {
        None = 0,

        Guitar   = 1 << 0,
        Bass     = 1 << 1,
        Drums    = 1 << 2,
        Vocals   = 1 << 3,
        Keyboard = 1 << 4,
    }

    /// <summary>
    /// A venue event involving the performers on-stage.
    /// </summary>
    public class PerformerEvent : VenueEvent, ICloneable<PerformerEvent>
    {
        public PerformerEventType Type { get; }
        public Performer Performers { get; }

        public PerformerEvent(PerformerEventType type, Performer performers,
            double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            Type = type;
            Performers = performers;
        }

        public PerformerEvent(PerformerEvent other) : base(other)
        {
            Type = other.Type;
            Performers = other.Performers;
        }

        public PerformerEvent Clone()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible types of performer events.
    /// </summary>
    public enum PerformerEventType
    {
        Spotlight,
        Singalong,
    }
}