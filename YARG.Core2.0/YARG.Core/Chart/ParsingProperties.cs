using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// The type of drums contained in the chart.
    /// </summary>
    public enum DrumsType
    {
        FourLane,
        ProDrums,
        FiveLane,
        Unknown,
        UnknownPro,
    }

    /// <summary>
    /// Settings used when parsing charts.
    /// </summary>
    public struct ParseSettings
    {
        /// <summary>
        /// The default settings to use for parsing.
        /// </summary>
        public static readonly ParseSettings Default = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = SETTING_DEFAULT,
            ChordHopoCancellation = false,
            StarPowerNote = SETTING_DEFAULT,
            NoteSnapThreshold = 0,
        };

        public static readonly ParseSettings Default_Chart = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = 0,
            ChordHopoCancellation = false,
            StarPowerNote = SETTING_DEFAULT,
            NoteSnapThreshold = 0,
        };

        public static readonly ParseSettings Default_Midi = new()
        {
            DrumsType = DrumsType.Unknown,
            HopoThreshold = SETTING_DEFAULT,
            SustainCutoffThreshold = SETTING_DEFAULT,
            ChordHopoCancellation = false,
            StarPowerNote = 116,
            NoteSnapThreshold = 0,
        };

        /// <summary>
        /// The value used to indicate a setting should be overwritten with the
        /// appropriate default value for the chart being parsed.
        /// </summary>
        public const int SETTING_DEFAULT = -1;

        /// <summary>
        /// The drums mode to parse the drums track as.
        /// </summary>
        public DrumsType DrumsType;

        /// <summary>
        /// The tick distance between notes to use as the HOPO threshold.
        /// </summary>
        /// <remarks>
        /// Uses the <c>hopo_threshold</c> tag from song.ini files.<br/>
        /// Defaults to a 1/12th note.
        /// </remarks>
        public long HopoThreshold;

        /// <summary>
        /// Skip marking single notes after chords as HOPOs
        /// if the single note shares a fret with the chord.
        /// </summary>
        public bool ChordHopoCancellation;

        /// <summary>
        /// The tick threshold to use for sustain cutoffs.
        /// </summary>
        /// <remarks>
        /// Uses the <c>sustain_cutoff_threshold</c> tag from song.ini files.<br/>
        /// Defaults to a 1/12th note in .mid, and 0 in .chart.
        /// </remarks>
        public long SustainCutoffThreshold;

        /// <summary>
        /// The tick threshold to use for snapping together single notes into chords.
        /// </summary>
        /// <remarks>
        /// Defaults to 10 in CON files, and 0 in other charts.
        /// </remarks>
        public long NoteSnapThreshold;

        /// <summary>
        /// The MIDI note to use for Star Power phrases in .mid charts.
        /// </summary>
        /// <remarks>
        /// Uses the <c>multiplier_note</c> and <c>star_power_note</c> tags from song.ini files.<br/>
        /// Defaults to 116.
        /// </remarks>
        public int StarPowerNote;
    }
}