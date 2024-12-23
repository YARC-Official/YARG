using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lyrics track.
    /// </summary>
    public class LyricsTrack : ICloneable<LyricsTrack>
    {
        public List<LyricsPhrase> Phrases { get; } = new();

        /// <summary>
        /// Whether or not any lyric phrases exist.
        /// </summary>
        public bool IsEmpty => Phrases.Count == 0;

        public LyricsTrack() { }

        public LyricsTrack(List<LyricsPhrase> parts)
        {
            Phrases = parts;
        }

        public LyricsTrack(LyricsTrack other)
            : this(other.Phrases.Duplicate())
        {
        }

        public double GetStartTime()
        {
            double totalStartTime = 0;

            if (Phrases.Count > 0)
                totalStartTime = Math.Min(Phrases[0].Time, totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;

            if (Phrases.Count > 0)
                totalEndTime = Math.Max(Phrases[^1].TimeEnd, totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            if (Phrases.Count > 0)
                totalFirstTick = Math.Min(Phrases[0].Tick, totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            if (Phrases.Count > 0)
                totalLastTick = Math.Max(Phrases[^1].TickEnd, totalLastTick);

            return totalLastTick;
        }

        public LyricsTrack Clone()
        {
            return new(this);
        }
    }
}