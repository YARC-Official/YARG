using System.Collections.Generic;

namespace YARG.Core.Game
{
    public partial class EnginePreset
    {
        public static EnginePreset Default = new("Default", true);

        public static EnginePreset Casual = new("Casual", true)
        {
            FiveFretGuitar =
            {
                AntiGhosting = false,
                InfiniteFrontEnd = true,
                StrumLeniency = 0.06,
                StrumLeniencySmall = 0.03
            },
            Vocals =
            {
                PerfectPitchPercent = 0.85f
            }
        };

        public static EnginePreset Precision = new("Precision", true)
        {
            FiveFretGuitar =
            {
                StrumLeniency = 0.04,
                StrumLeniencySmall = 0.02,
                HitWindow =
                {
                    MaxWindow = 0.12,
                    MinWindow = 0.04,
                    IsDynamic = true,
                    DynamicScale = 1,
                    DynamicSlope = 0.93,
                    DynamicGamma = 1.5,
                }
            },
            Drums =
            {
                HitWindow =
                {
                    MaxWindow = 0.13,
                    MinWindow = 0.05,
                    IsDynamic = true,
                    DynamicScale = 1,
                    DynamicSlope = 0.60615,
                    DynamicGamma = 2
                }
            },
            Vocals =
            {
                PitchWindowE = 1.4f,
                PitchWindowM = 1.1f,
                PitchWindowH = 0.8f,
                PitchWindowX = 0.6f,
                PerfectPitchPercent = 0.55f
            }
        };

        public static readonly List<EnginePreset> Defaults = new()
        {
            Default,
            Casual,
            Precision
        };
    }
}