using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lighting event for the stage of a venue.
    /// </summary>
    public class LightingEvent : VenueEvent, ICloneable<LightingEvent>
    {
        public LightingType Type { get; }

        public LightingEvent(LightingType type, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public LightingEvent(LightingEvent other) : base(other)
        {
            Type = other.Type;
        }

        public LightingEvent Clone()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible lighting types.
    /// </summary>
    public enum LightingType
    {
        // Keyframed
        Default,
        Dischord,
        Chorus,
        Cool_Manual,
        Stomp,
        Verse,
        Warm_Manual,

        // Automatic
        BigRockEnding,
        Blackout_Fast,
        Blackout_Slow,
        Blackout_Spotlight,
        Cool_Automatic,
        Flare_Fast,
        Flare_Slow,
        Frenzy,
        Intro,
        Harmony,
        Silhouettes,
        Silhouettes_Spotlight,
        Searchlights,
        Strobe_Fastest,
        Strobe_Fast,
        Strobe_Medium,
        Strobe_Slow,
        Strobe_Off,
        Sweep,
        Warm_Automatic,

        // Keyframe events
        Keyframe_First,
        Keyframe_Next,
        Keyframe_Previous,

        //YARG internal
        Menu,
        Score,
        NoCue,
    }
}
