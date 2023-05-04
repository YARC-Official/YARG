// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class BPM : SyncTrack
    {
        private readonly ID _classID = ID.BPM;

        public override int classID { get { return (int)_classID; } }

        /// <summary>
        /// Stored as the bpm value * 1000. For example, a bpm of 120.075 would be stored as 120075.
        /// </summary>
        public uint value;
        public float displayValue
        {
            get
            {
                return (float)value / 1000.0f;
            }
        }

        public double? anchor = null;

        /// <summary>
        /// Basic constructor.
        /// </summary>
        /// <param name="_position">Tick position.</param>
        /// <param name="_value">Stored as the bpm value * 1000 to limit it to 3 decimal places. For example, a bpm of 120.075 would be stored as 120075.</param>
        public BPM(uint _position = 0, uint _value = 120000, float? _anchor = null) : base(_position)
        {
            value = _value;
            anchor = _anchor;
        }

        public BPM(BPM _bpm) : base(_bpm.tick)
        {
            value = _bpm.value;
            anchor = _bpm.anchor;
        }

        public double assignedTime = 0;

        public override SongObject Clone()
        {
            return new BPM(this);
        }

        public override bool AllValuesCompare<T>(T songObject)
        {
            if (this == songObject && songObject as BPM != null && (songObject as BPM).value == value)
                return true;
            else
                return false;
        }

        public void CopyFrom(BPM bpm)
        {
            tick = bpm.tick;
            value = bpm.value;
            anchor = bpm.anchor;
        }
    }
}
