using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A text event used for chart lyrics.
    /// </summary>
    public class LyricEvent : ChartEvent, ICloneable<LyricEvent>
    {
        private readonly LyricSymbolFlags _flags;

        public string Text { get; }

        public LyricSymbolFlags Flags => _flags;

        public bool JoinWithNext  => (_flags & LyricSymbolFlags.JoinWithNext) != 0;
        public bool NonPitched    => (_flags & LyricSymbolFlags.NonPitched) != 0;
        public bool PitchSlide    => (_flags & LyricSymbolFlags.PitchSlide) != 0;
        public bool HarmonyHidden => (_flags & LyricSymbolFlags.HarmonyHidden) != 0;
        public bool StaticShift   => (_flags & LyricSymbolFlags.StaticShift) != 0;

        // Range shifts are handled externally
        // public bool RangeShift => (_flags & LyricFlags.RangeShift) != 0;

        public LyricEvent(LyricSymbolFlags flags, string text, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            _flags = flags;
            Text = text;
        }

        public LyricEvent(LyricEvent other) : base(other)
        {
            _flags = other._flags;
            Text = other.Text;
        }

        public LyricEvent Clone()
        {
            return new(this);
        }

        public override string ToString()
        {
            return $"Lyric event '{Text}' at {Time}s ({Tick}t) with flags {_flags}";
        }
    }
}