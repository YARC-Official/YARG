using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lyric/percussion phrase on a vocals track.
    /// </summary>
    public class VocalsPhrase : ChartEvent, ICloneable<VocalsPhrase>
    {
        public VocalNote PhraseParentNote { get; }
        public List<LyricEvent> Lyrics { get; }

        public bool IsLyric => !PhraseParentNote.IsPercussion;
        public bool IsPercussion => PhraseParentNote.IsPercussion;

        public bool IsStarPower => PhraseParentNote.IsStarPower;

        public VocalsPhrase(double time, double timeLength, uint tick, uint tickLength,
            VocalNote phraseParentNote, List<LyricEvent> lyrics)
            : base(time, timeLength, tick, tickLength)
        {
            if (!phraseParentNote.IsPhrase)
            {
                throw new InvalidOperationException(
                    "Attempted to create a vocals phrase out of a non-phrase vocals note!");
            }

            PhraseParentNote = phraseParentNote;
            Lyrics = lyrics;
        }

        public VocalsPhrase(VocalsPhrase other)
            : base(other)
        {
            PhraseParentNote = other.PhraseParentNote.Clone();
            Lyrics = other.Lyrics.Duplicate();
        }

        public VocalsPhrase Clone()
        {
            return new(this);
        }
    }
}