using System;

namespace YARG.Core.Chart
{
    public partial class Beatline : SyncEvent, IEquatable<Beatline>, ICloneable<Beatline>
    {
        public BeatlineType Type { get; }

        public Beatline(BeatlineType type, double time, uint tick) : base(time, tick)
        {
            Type = type;
        }

        public Beatline Clone()
        {
            return new(Type, Time, Tick);
        }

        public static bool operator ==(Beatline? left, Beatline? right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (left is null || right is null)
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(Beatline? left, Beatline? right)
            => !(left == right);

        public bool Equals(Beatline other)
        {
            return base.Equals(other) && Type == other.Type;
        }

        public override bool Equals(object? obj)
            => obj is Beatline tempo && Equals(tempo);

        public override int GetHashCode()
            => base.GetHashCode();

        public override string ToString()
        {
            return $"{Type} line at tick {Tick}, time {Time}";
        }
    }

    public enum BeatlineType
    {
        Measure,
        Strong,
        Weak,
    }
}