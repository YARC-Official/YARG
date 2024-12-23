using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A phrase within the lyrics track.
    /// </summary>
    public class LyricsPhrase : ChartEvent, ICloneable<LyricsPhrase>
    {
        public List<LyricEvent> Lyrics { get; } = new();

        public LyricsPhrase(double time, double timeLength, uint tick, uint tickLength, List<LyricEvent> lyrics)
            : base(time, timeLength, tick, tickLength)
        {
            Lyrics = lyrics;
        }

        public LyricsPhrase(LyricsPhrase other)
            : base(other)
        {
            Lyrics = other.Lyrics.Duplicate();
        }

        public LyricsPhrase Clone()
        {
            return new(this);
        }
    }
}