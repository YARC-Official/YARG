// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class TimeSignature : SyncTrack
    {
        private readonly ID _classID = ID.TimeSignature;

        public override int classID { get { return (int)_classID; } }

        public uint numerator;
        public uint denominator;

        public uint quarterNotesPerMeasure { get { return numerator; } }
        public uint beatsPerMeasure { get { return denominator; } }

        public TimeSignature(uint _position = 0, uint _numerator = 4, uint _denominator = 4) : base(_position)
        {
            numerator = _numerator;
            denominator = _denominator;
        }

        public TimeSignature(TimeSignature ts) : base(ts.tick)
        {
            numerator = ts.numerator;
            denominator = ts.denominator;
        }

        public override SongObject Clone()
        {
            return new TimeSignature(this);
        }

        public override bool AllValuesCompare<T>(T songObject)
        {
            if (this == songObject && songObject as TimeSignature != null && (songObject as TimeSignature).numerator == numerator)
                return true;
            else
                return false;
        }

        public void CopyFrom(TimeSignature ts)
        {
            tick = ts.tick;
            numerator = ts.numerator;
            denominator = ts.denominator;
        }

        public struct BeatInfo
        {
            public uint tickOffset;
            public uint tickGap;
            public int repetitions;
            public uint repetitionCycleOffset;
        }

        public struct MeasureInfo
        {
            public BeatInfo measureLine;
            public BeatInfo beatLine;
            public BeatInfo quarterBeatLine;
        }

        public MeasureInfo GetMeasureInfo()
        {
            MeasureInfo measureInfo = new MeasureInfo();
            float resolution = moonSong.resolution;

            {
                measureInfo.measureLine.tickOffset = 0;
                measureInfo.measureLine.repetitions = 1;
                measureInfo.measureLine.tickGap = (uint)(resolution * 4.0f / denominator * numerator);
                measureInfo.measureLine.repetitionCycleOffset = 0;
            }

            {
                measureInfo.beatLine.tickGap = measureInfo.measureLine.tickGap / numerator;
                measureInfo.beatLine.tickOffset = measureInfo.beatLine.tickGap;
                measureInfo.beatLine.repetitions = (int)numerator - 1;
                measureInfo.beatLine.repetitionCycleOffset = measureInfo.beatLine.tickOffset;
            }

            {
                measureInfo.quarterBeatLine.tickGap = measureInfo.beatLine.tickGap;
                measureInfo.quarterBeatLine.tickOffset = measureInfo.beatLine.tickGap / 2;
                measureInfo.quarterBeatLine.repetitions = (int)numerator;
                measureInfo.quarterBeatLine.repetitionCycleOffset = 0;
            }

            return measureInfo;
        }
    }
}
