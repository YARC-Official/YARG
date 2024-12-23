using System;
using System.Collections.Generic;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A venue track.
    /// </summary>
    public class VenueTrack
    {
        public List<LightingEvent> Lighting { get; } = new();
        public List<PostProcessingEvent> PostProcessing { get; } = new();
        public List<PerformerEvent> Performer { get; } = new();
        public List<StageEffectEvent> Stage { get; } = new();

        public VenueTrack() { }

        public VenueTrack(List<LightingEvent> lighting, List<PostProcessingEvent> postProcessing,
            List<PerformerEvent> performer, List<StageEffectEvent> stage)
        {
            Lighting = lighting;
            PostProcessing = postProcessing;
            Performer = performer;
            Stage = stage;
        }

        public double GetStartTime()
        {
            double totalStartTime = 0;

            totalStartTime = Math.Min(Lighting.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(PostProcessing.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(Performer.GetStartTime(), totalStartTime);
            totalStartTime = Math.Min(Stage.GetStartTime(), totalStartTime);

            return totalStartTime;
        }

        public double GetEndTime()
        {
            double totalEndTime = 0;

            totalEndTime = Math.Max(Lighting.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(PostProcessing.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(Performer.GetEndTime(), totalEndTime);
            totalEndTime = Math.Max(Stage.GetEndTime(), totalEndTime);

            return totalEndTime;
        }

        public uint GetFirstTick()
        {
            uint totalFirstTick = 0;

            totalFirstTick = Math.Min(Lighting.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(PostProcessing.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Performer.GetFirstTick(), totalFirstTick);
            totalFirstTick = Math.Min(Stage.GetFirstTick(), totalFirstTick);

            return totalFirstTick;
        }

        public uint GetLastTick()
        {
            uint totalLastTick = 0;

            totalLastTick = Math.Max(Lighting.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(PostProcessing.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Performer.GetLastTick(), totalLastTick);
            totalLastTick = Math.Max(Stage.GetLastTick(), totalLastTick);

            return totalLastTick;
        }
    }
}