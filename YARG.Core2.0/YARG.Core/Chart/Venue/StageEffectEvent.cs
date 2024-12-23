using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// Miscellaneous stage effects for venues.
    /// </summary>
    public class StageEffectEvent : VenueEvent, ICloneable<StageEffectEvent>
    {
        public StageEffect Effect { get; }

        public StageEffectEvent(StageEffect effect, VenueEventFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0)
        {
            Effect = effect;
        }

        public StageEffectEvent(StageEffectEvent other) : base(other)
        {
            Effect = other.Effect;
        }

        public StageEffectEvent Clone()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible stage effects.
    /// </summary>
    public enum StageEffect
    {
        BonusFx,
        FogOn,
        FogOff,
    }
}