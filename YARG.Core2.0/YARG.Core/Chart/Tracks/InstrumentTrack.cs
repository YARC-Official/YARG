using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace YARG.Core.Chart
{
    /// <summary>
    /// An instrument track and all of its difficulties.
    /// </summary>
    public class InstrumentTrack<TNote> : ICloneable<InstrumentTrack<TNote>>
        where TNote : Note<TNote>
    {
        public Instrument Instrument { get; }

        private Dictionary<Difficulty, InstrumentDifficulty<TNote>> _difficulties { get; } = new();

        /// <summary>
        /// Whether or not this track contains any data.
        /// </summary>
        public bool IsEmpty
        {
            get
            {
                foreach (var difficulty in _difficulties.Values)
                {
                    if (!difficulty.IsEmpty)
                        return false;
                }

                return true;
            }
        }

        public InstrumentTrack(Instrument instrument)
        {
            Instrument = instrument;
        }

        public InstrumentTrack(Instrument instrument, Dictionary<Difficulty, InstrumentDifficulty<TNote>> difficulties)
            : this(instrument)
        {
            _difficulties = difficulties;
        }

        public InstrumentTrack(InstrumentTrack<TNote> other)
            : this(other.Instrument)
        {
            foreach (var (difficulty, diffTrack) in other._difficulties)
            {
                _difficulties.Add(difficulty, diffTrack.Clone());
            }
        }

        public void AddDifficulty(Difficulty difficulty, InstrumentDifficulty<TNote> track)
            => _difficulties.Add(difficulty, track);

        public void RemoveDifficulty(Difficulty difficulty)
            => _difficulties.Remove(difficulty);

        public InstrumentDifficulty<TNote> GetDifficulty(Difficulty difficulty)
            => _difficulties[difficulty];

        public bool TryGetDifficulty(Difficulty difficulty, [NotNullWhen(true)] out InstrumentDifficulty<TNote>? track)
            => _difficulties.TryGetValue(difficulty, out track);

        // For unit tests
        internal InstrumentDifficulty<TNote> FirstDifficulty()
            => _difficulties.First().Value;

        public double GetStartTime()
        {
            double totalStartTime = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalStartTime = Math.Min(difficulty.GetStartTime(), totalStartTime);
            }

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalEndTime = Math.Max(difficulty.GetEndTime(), totalEndTime);
            }

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalFirstTick = Math.Min(difficulty.GetFirstTick(), totalFirstTick);
            }

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;
            foreach (var difficulty in _difficulties.Values)
            {
                totalLastTick = Math.Max(difficulty.GetLastTick(), totalLastTick);
            }

            return totalLastTick;
        }

        public InstrumentTrack<TNote> Clone()
        {
            return new(this);
        }
    }
}