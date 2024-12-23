using System;
using System.Runtime.CompilerServices;

namespace YARG.Core.Chart
{
    public class TempoChange : SyncEvent, IEquatable<TempoChange>, ICloneable<TempoChange>
    {
        private const float SECONDS_PER_MINUTE = 60f;

        public float BeatsPerMinute { get; }
        public float SecondsPerBeat => SECONDS_PER_MINUTE / BeatsPerMinute;
        public long MilliSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute) / 1000;
        public long MicroSecondsPerBeat => BpmToMicroSeconds(BeatsPerMinute);

        public TempoChange(float tempo, double time, uint tick) : base(time, tick)
        {
            BeatsPerMinute = tempo;
        }

        public TempoChange Clone()
        {
            return new(BeatsPerMinute, Time, Tick);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long BpmToMicroSeconds(float tempo)
        {
            double secondsPerBeat = SECONDS_PER_MINUTE / tempo;
            double microseconds = secondsPerBeat * 1000 * 1000;
            return (long) microseconds;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float MicroSecondsToBpm(long usecs)
        {
            double secondsPerBeat = usecs / 1000f / 1000f;
            double tempo = SECONDS_PER_MINUTE / secondsPerBeat;
            return (float) tempo;
        }

        public static bool operator ==(TempoChange? left, TempoChange? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TempoChange? left, TempoChange? right)
            => !(left == right);

        public bool Equals(TempoChange other)
        {
            return base.Equals(other) && BeatsPerMinute == other.BeatsPerMinute;
        }

        public override bool Equals(object? obj)
            => obj is TempoChange tempo && Equals(tempo);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"Tempo {BeatsPerMinute} at tick {Tick}, time {Time}";
        }
    }
}