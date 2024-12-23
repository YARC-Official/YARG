using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Possible phrase types.
    /// </summary>
    public enum PhraseType
    {
        // Note modifiers
        StarPower,   // Mainly for visuals, notes are already marked directly as SP
        TremoloLane, // Guitar strum lanes, single drum rolls
        TrillLane,   // Guitar trill lanes, double drum rolls
        DrumFill,    // Also for visuals

        // Versus modes (face-off and the like)
        VersusPlayer1,
        VersusPlayer2,

        // Other events
        Solo, // Also for visuals
        BigRockEnding,

        // Pro-keys range shifts
        ProKeys_RangeShift0,
        ProKeys_RangeShift1,
        ProKeys_RangeShift2,
        ProKeys_RangeShift3,
        ProKeys_RangeShift4,
        ProKeys_RangeShift5,
    }

    /// <summary>
    /// A phrase event that occurs in a chart.
    /// </summary>
    public class Phrase : ChartEvent, ICloneable<Phrase>
    {
        public PhraseType Type { get; }

        public Phrase(PhraseType type, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            Type = type;
        }

        public Phrase(Phrase other) : base(other)
        {
            Type = other.Type;
        }

        public Phrase Clone()
        {
            return new(this);
        }
    }
}