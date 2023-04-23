// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song.IO
{
    public struct ExportOptions
    {
        public struct MidiOptions
        {
            public enum RBFormat
            {
                RB2,
                RB3,
            }

            public MoonSong.Difficulty difficultyToUseGlobalTrackEvents;   // Which difficulty to take things like starpower, tap, solo and tom toggle events from
            public RBFormat rbFormat;      // Changes section name prefix
        }

        public bool forced;
        public Format format;
        public uint tickOffset;
        public float targetResolution;
        public bool copyDownEmptyDifficulty;
        public MidiOptions midiOptions;
        public bool isGeneralSave;
        public bool substituteCHLyricChars;

        public enum Format
        {
            Chart, Midi, Msce
        }

        public enum Game
        {
            PhaseShift, RockBand2, RockBand3
        }
    }
}
