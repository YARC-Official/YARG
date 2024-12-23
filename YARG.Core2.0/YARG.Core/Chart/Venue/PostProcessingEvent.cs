using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A lighting event for the stage of a venue.
    /// </summary>
    public class PostProcessingEvent : VenueEvent, ICloneable<PostProcessingEvent>
    {
        public PostProcessingType Type { get; }

        public PostProcessingEvent(PostProcessingType type, double time, uint tick)
            : base(time, 0, tick, 0)
        {
            Type = type;
        }

        public PostProcessingEvent(PostProcessingEvent other) : base(other)
        {
            Type = other.Type;
        }

        public PostProcessingEvent Clone()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible post-processing effects.
    /// </summary>
    public enum PostProcessingType
    {
        // Basic effects
        Default,
        Bloom,
        Bright,
        Contrast,
        Posterize,
        PhotoNegative,
        Mirror,

        // Color filters/effects
        BlackAndWhite,
        SepiaTone,
        SilverTone,

        Choppy_BlackAndWhite,
        PhotoNegative_RedAndBlack,
        Polarized_BlackAndWhite,
        Polarized_RedAndBlue,

        Desaturated_Blue,
        Desaturated_Red,

        Contrast_Red,
        Contrast_Green,
        Contrast_Blue,

        // Grainy
        Grainy_Film,
        Grainy_ChromaticAbberation,

        // Scanlines
        Scanlines,
        Scanlines_BlackAndWhite,
        Scanlines_Blue,
        Scanlines_Security,

        // Trails
        Trails,
        Trails_Long,
        Trails_Desaturated,
        Trails_Flickery,
        Trails_Spacey,
    }
}