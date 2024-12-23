using System;

namespace YARG.Core.Chart
{
    public partial class TimeSignatureChange : SyncEvent, IEquatable<TimeSignatureChange>, ICloneable<TimeSignatureChange>
    {
        public const float QUARTER_NOTE_DENOMINATOR = 4f;

        public uint Numerator   { get; }
        public uint Denominator { get; }

        public TimeSignatureChange(uint numerator, uint denominator, double time, uint tick) : base(time, tick)
        {
            Numerator = numerator;
            Denominator = denominator;
        }

        public TimeSignatureChange Clone()
        {
            return new(Numerator, Denominator, Time, Tick);
        }

        /// <summary>
        /// Calculates the number of ticks per beat for this time signature.
        /// </summary>
        public uint GetTicksPerBeat(SyncTrack sync)
        {
            return (uint) (sync.Resolution * (QUARTER_NOTE_DENOMINATOR / Denominator));
        }

        /// <summary>
        /// Calculates the number of ticks per measure for this time signature.
        /// </summary>
        public uint GetTicksPerMeasure(SyncTrack sync)
        {
            return GetTicksPerBeat(sync) * Numerator;
        }

        // For template generation purposes
        private static uint GetTicksPerQuarterNote(SyncTrack sync)
        {
            return sync.Resolution;
        }

        /// <summary>
        /// Calculates the number of seconds per beat for this time signature.
        /// </summary>
        public double GetSecondsPerBeat(TempoChange tempo)
        {
            return tempo.SecondsPerBeat * (QUARTER_NOTE_DENOMINATOR / Denominator);
        }

        /// <summary>
        /// Calculates the number of seconds per measure for this time signature.
        /// </summary>
        public double GetSecondsPerMeasure(TempoChange tempo)
        {
            return GetSecondsPerBeat(tempo) * Numerator;
        }

        public static bool operator ==(TimeSignatureChange? left, TimeSignatureChange? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TimeSignatureChange? left, TimeSignatureChange? right)
            => !(left == right);

        public bool Equals(TimeSignatureChange other)
        {
            return base.Equals(other) &&
                Numerator == other.Numerator &&
                Denominator == other.Denominator;
        }

        public override bool Equals(object? obj)
            => obj is TimeSignatureChange timeSig && Equals(timeSig);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"Time signature {Numerator}/{Denominator} at tick {Tick}, time {Time}";
        }
    }
}