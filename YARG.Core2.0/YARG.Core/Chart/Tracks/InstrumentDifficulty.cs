using System;
using System.Collections.Generic;
using YARG.Core.Extensions;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A single difficulty of an instrument track.
    /// </summary>
    public class InstrumentDifficulty<TNote> : ICloneable<InstrumentDifficulty<TNote>>
        where TNote : Note<TNote>
    {
        public Instrument Instrument { get; }
        public Difficulty Difficulty { get; }

        public List<TNote> Notes { get; } = new();
        public List<Phrase> Phrases { get; } = new();
        public List<TextEvent> TextEvents { get; } = new();

        /// <summary>
        /// Whether or not this difficulty contains any data.
        /// </summary>
        /// <remarks>
        /// This should *not* be used to determine whether or not the chart is present!
        /// Use <see cref="InstrumentTrack{TNote}.TryGetDifficulty(Difficulty, out InstrumentDifficulty{TNote}?)"/> instead.
        /// </remarks>
        public bool IsEmpty => Notes.Count == 0 && Phrases.Count == 0 && TextEvents.Count == 0;

        public InstrumentDifficulty(Instrument instrument, Difficulty difficulty)
        {
            Instrument = instrument;
            Difficulty = difficulty;
        }

        public InstrumentDifficulty(Instrument instrument, Difficulty difficulty,
            List<TNote> notes, List<Phrase> phrases, List<TextEvent> text)
            : this(instrument, difficulty)
        {
            Notes = notes;
            Phrases = phrases;
            TextEvents = text;
        }

        public InstrumentDifficulty(InstrumentDifficulty<TNote> other)
            : this(other.Instrument, other.Difficulty, other.Notes.DuplicateNotes(), other.Phrases.Duplicate(),
                other.TextEvents.Duplicate())
        {
        }

        public double GetStartTime()
        {
            double totalStartTime = 0;

            totalStartTime = Math.Min(Notes.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(Phrases.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(TextEvents.GetStartTime(), totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;

            totalEndTime = Math.Max(Notes.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(Phrases.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(TextEvents.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            totalFirstTick = Math.Min(Notes.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Phrases.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(TextEvents.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Notes.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Phrases.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(TextEvents.GetLastTick(), totalLastTick);

            return totalLastTick;
        }

        public InstrumentDifficulty<TNote> Clone()
        {
            return new(this);
        }

        public int GetTotalNoteCount()
        {
            var noteCount = 0;
            foreach (var note in Notes)
            {
                noteCount += note.ChildNotes.Count + 1;
            }

            return noteCount;
        }
    }
}