using System;
using YARG.Core.Engine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.ProKeys;
using YARG.Core.Engine.Vocals;
using YARG.Core.Game.Settings;

namespace YARG.Core.Game
{
    public partial class EnginePreset
    {
        public const double DEFAULT_WHAMMY_BUFFER = 0.25;

        public const double DEFAULT_SUSTAIN_DROP_LENIENCY = 0.025;

        public const int DEFAULT_MAX_MULTIPLIER = 4;
        public const int BASS_MAX_MULTIPLIER    = 6;

        /// <summary>
        /// A preset for a hit window. This should
        /// be used within each engine preset class.
        /// </summary>
        public class HitWindowPreset
        {
            // These should be ignored from the settings because they are handled separately.
            // Everything else will only appear when "IsDynamic" is true.
            public bool   IsDynamic = false;
            public double MaxWindow = 0.14;
            public double MinWindow = 0.14;

            [SettingType(SettingType.Slider)]
            [SettingRange(0.3f, 3f)]
            public double DynamicScale = 1.0;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public double DynamicSlope = 0.93;

            [SettingType(SettingType.Slider)]
            [SettingRange(0.1f, 10f)]
            public double DynamicGamma = 1.5;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 2f)]
            public double FrontToBackRatio = 1.0;

            public HitWindowSettings Create()
            {
                return new HitWindowSettings(MaxWindow, MinWindow, FrontToBackRatio, IsDynamic,
                    DynamicSlope, DynamicScale, DynamicGamma);
            }

            public HitWindowPreset Copy()
            {
                return new HitWindowPreset
                {
                    IsDynamic = IsDynamic,
                    MaxWindow = MaxWindow,
                    MinWindow = MinWindow,

                    DynamicScale = DynamicScale,
                    DynamicSlope = DynamicSlope,
                    DynamicGamma = DynamicGamma,

                    FrontToBackRatio = FrontToBackRatio
                };
            }
        }

        /// <summary>
        /// The engine preset for five fret guitar.
        /// </summary>
        public class FiveFretGuitarPreset
        {
            [SettingType(SettingType.Toggle)]
            public bool AntiGhosting = true;

            [SettingType(SettingType.Toggle)]
            public bool InfiniteFrontEnd = false;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double HopoLeniency = 0.08;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double StrumLeniency = 0.05;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double StrumLeniencySmall = 0.025;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f, max: 0.05f)]
            public double SustainDropLeniency = DEFAULT_SUSTAIN_DROP_LENIENCY;

            [SettingType(SettingType.Special)]
            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.14,
                MinWindow = 0.14,
                IsDynamic = false,
                FrontToBackRatio = 1.0
            };

            public FiveFretGuitarPreset Copy()
            {
                return new FiveFretGuitarPreset
                {
                    AntiGhosting = AntiGhosting,
                    InfiniteFrontEnd = InfiniteFrontEnd,
                    HopoLeniency = HopoLeniency,
                    StrumLeniency = StrumLeniency,
                    StrumLeniencySmall = StrumLeniencySmall,
                    HitWindow = HitWindow.Copy(),
                };
            }

            public GuitarEngineParameters Create(float[] starMultiplierThresholds, bool isBass)
            {
                var hitWindow = HitWindow.Create();
                return new GuitarEngineParameters(
                    hitWindow,
                    isBass ? BASS_MAX_MULTIPLIER : DEFAULT_MAX_MULTIPLIER,
                    DEFAULT_WHAMMY_BUFFER,
                    SustainDropLeniency,
                    starMultiplierThresholds,
                    HopoLeniency,
                    StrumLeniency,
                    StrumLeniencySmall,
                    InfiniteFrontEnd,
                    AntiGhosting);
            }
        }

        /// <summary>
        /// The engine preset for four and five lane drums. These two game modes
        /// use the same engine, so there's no point in splitting them up.
        /// </summary>
        public class DrumsPreset
        {
            [SettingType(SettingType.Special)]
            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.14,
                MinWindow = 0.14,
                IsDynamic = false,
                FrontToBackRatio = 1.0
            };

            public DrumsPreset Copy()
            {
                return new DrumsPreset
                {
                    HitWindow = HitWindow.Copy()
                };
            }

            public DrumsEngineParameters Create(float[] starMultiplierThresholds, DrumsEngineParameters.DrumMode mode)
            {
                var hitWindow = HitWindow.Create();
                return new DrumsEngineParameters(
                    hitWindow,
                    DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds,
                    mode);
            }
        }

        /// <summary>
        /// The engine preset for vocals/harmonies.
        /// </summary>
        public class VocalsPreset
        {
            // Pitch window is in semitones (max. difference between correct pitch and sung pitch).

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 3f)]
            public float PitchWindowE = 1.7f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 3f)]
            public float PitchWindowM = 1.4f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 3f)]
            public float PitchWindowH = 1.1f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 3f)]
            public float PitchWindowX = 0.8f;

            /// <summary>
            /// The perfect pitch window is equal to the pitch window times the perfect pitch percent,
            /// for all difficulties.
            /// </summary>
            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public float PerfectPitchPercent = 0.6f;

            // These percentages may seem low, but accounting for delay,
            // plosives not being detected, etc., it's pretty good.

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public float HitPercentE = 0.325f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public float HitPercentM = 0.400f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public float HitPercentH = 0.450f;

            [SettingType(SettingType.Slider)]
            [SettingRange(0f, 1f)]
            public float HitPercentX = 0.575f;

            /// <summary>
            /// The hit window of percussion notes.
            /// </summary>
            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double PercussionHitWindow = 0.16;

            public VocalsPreset Copy()
            {
                return new VocalsPreset
                {
                    PitchWindowE = PitchWindowE,
                    PitchWindowM = PitchWindowM,
                    PitchWindowH = PitchWindowH,
                    PitchWindowX = PitchWindowX,
                    PerfectPitchPercent = PerfectPitchPercent,
                    HitPercentE = HitPercentE,
                    HitPercentM = HitPercentM,
                    HitPercentH = HitPercentH,
                    HitPercentX = HitPercentX,
                };
            }

            public VocalsEngineParameters Create(float[] starMultiplierThresholds, Difficulty difficulty,
                float updatesPerSecond, bool singToActivateStarPower)
            {
                // Hit window is in semitones (max. difference between correct pitch and sung pitch).
                var (pitchWindow, hitPercent, pointsPerPhrase) = difficulty switch
                {
                    Difficulty.Easy   => (PitchWindowE, HitPercentE, 400),
                    Difficulty.Medium => (PitchWindowM, HitPercentM, 800),
                    Difficulty.Hard   => (PitchWindowH, HitPercentH, 1600),
                    Difficulty.Expert => (PitchWindowX, HitPercentX, 2000),
                    _                 => throw new InvalidOperationException("Unreachable")
                };

                var hitWindow = new HitWindowSettings(
                    PercussionHitWindow, PercussionHitWindow, 1, false, 0, 0, 0);

                return new VocalsEngineParameters(
                    hitWindow,
                    DEFAULT_MAX_MULTIPLIER,
                    starMultiplierThresholds,
                    pitchWindow,
                    pitchWindow * PerfectPitchPercent,
                    hitPercent,
                    updatesPerSecond,
                    singToActivateStarPower,
                    pointsPerPhrase);
            }
        }

        /// <summary>
        /// The engine preset for pro keys.
        /// </summary>
        public class ProKeysPreset
        {
            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double ChordStaggerWindow = 0.05;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f)]
            public double FatFingerWindow = 0.1;

            [SettingType(SettingType.MillisecondInput)]
            [SettingRange(min: 0f, max: 0.05f)]
            public double SustainDropLeniency = DEFAULT_SUSTAIN_DROP_LENIENCY;

            [SettingType(SettingType.Special)]
            public HitWindowPreset HitWindow = new()
            {
                MaxWindow = 0.14,
                MinWindow = 0.14,
                IsDynamic = false,
                FrontToBackRatio = 1.0
            };

            public ProKeysPreset Copy()
            {
                return new ProKeysPreset
                {
                    ChordStaggerWindow = ChordStaggerWindow,
                    FatFingerWindow = FatFingerWindow,
                    HitWindow = HitWindow.Copy(),
                };
            }

            public ProKeysEngineParameters Create(float[] starMultiplierThresholds)
            {
                var hitWindow = HitWindow.Create();
                return new ProKeysEngineParameters(
                    hitWindow,
                    DEFAULT_MAX_MULTIPLIER,
                    DEFAULT_WHAMMY_BUFFER,
                    SustainDropLeniency,
                    starMultiplierThresholds,
                    ChordStaggerWindow,
                    FatFingerWindow);
            }
        }
    }
}
