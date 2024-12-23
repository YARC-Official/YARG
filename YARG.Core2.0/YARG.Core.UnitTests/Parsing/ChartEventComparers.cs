using System.Diagnostics.CodeAnalysis;
using YARG.Core.Chart;

namespace YARG.Core.UnitTests.Parsing
{
    public abstract class ChartEventComparer<TEvent> : IEqualityComparer<TEvent>
        where TEvent : ChartEvent
    {
        public bool Equals(TEvent? x, TEvent? y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            if (x.Tick != y.Tick ||
                x.TickLength != y.TickLength ||
                Math.Abs(x.Time - y.Time) >= 0.001 ||
                Math.Abs(x.TimeLength - y.TimeLength) >= 0.001)
                return false;

            return EqualsImpl(x, y);
        }

        protected abstract bool EqualsImpl([DisallowNull] TEvent x, [DisallowNull] TEvent y);

        public int GetHashCode([DisallowNull] TEvent obj) => obj.GetHashCode();
    }

    public sealed class ChartEventComparer : ChartEventComparer<ChartEvent>
    {
        protected override bool EqualsImpl([DisallowNull] ChartEvent x, [DisallowNull] ChartEvent y)
            // The base will have already determined all of ChartEvent's properties to be equal by now
            => true;
    }

    public sealed class LyricEventComparer : ChartEventComparer<LyricEvent>
    {
        protected override bool EqualsImpl([DisallowNull] LyricEvent x, [DisallowNull] LyricEvent y)
        {
            return x.Text == y.Text && x.Flags == y.Flags;
        }
    }
}