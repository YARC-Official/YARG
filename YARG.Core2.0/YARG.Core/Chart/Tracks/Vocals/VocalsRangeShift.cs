using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A range shift on the vocals track.
    /// </summary>
    public class VocalsRangeShift : ChartEvent, ICloneable<VocalsRangeShift>
    {
        public float MinimumPitch { get; }
        public float MaximumPitch { get; }

        public VocalsRangeShift(float minPitch, float maxPitch,
            double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            MinimumPitch = minPitch;
            MaximumPitch = maxPitch;
        }

        public VocalsRangeShift(VocalsRangeShift other)
            : base(other)
        {
            MinimumPitch = other.MinimumPitch;
            MaximumPitch = other.MaximumPitch;
        }

        public VocalsRangeShift Clone()
        {
            return new(this);
        }
    }
}