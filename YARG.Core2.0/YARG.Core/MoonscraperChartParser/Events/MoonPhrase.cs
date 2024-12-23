// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonPhrase : MoonObject
    {
        public enum Type
        {
            Starpower,
            Solo,

            Versus_Player1,
            Versus_Player2,

            TremoloLane,
            TrillLane,

            // RB Pro Drums

            ProDrums_Activation,

            // Vocals

            Vocals_LyricPhrase,
            Vocals_PercussionPhrase,
            Vocals_RangeShift,
            Vocals_LyricShift,

            // Pro keys

            ProKeys_RangeShift0,
            ProKeys_RangeShift1,
            ProKeys_RangeShift2,
            ProKeys_RangeShift3,
            ProKeys_RangeShift4,
            ProKeys_RangeShift5,

            ProKeys_Glissando,
        }

        public uint length;
        public Type type;

        public MoonPhrase(uint _position, uint _length, Type _type)
            : base(ID.Phrase, _position)
        {
            length = _length;
            type = _type;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonPhrase phrase)
                return baseEq;

            return type == phrase.type;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonPhrase phrase)
                return baseComp;

            return ((int) type).CompareTo((int) phrase.type);
        }

        public uint GetCappedLengthForPos(uint pos, MoonChart? chart)
        {
            uint newLength;
            if (pos > tick)
                newLength = pos - tick;
            else
                newLength = 0;

            MoonPhrase? nextSp = null;
            if (chart != null)
            {
                int arrayPos = MoonObjectHelper.FindClosestPosition(this, chart.specialPhrases);
                if (arrayPos == MoonObjectHelper.NOTFOUND)
                    return newLength;

                while (arrayPos < chart.specialPhrases.Count - 1 && chart.specialPhrases[arrayPos].tick <= tick)
                {
                    ++arrayPos;
                }

                if (chart.specialPhrases[arrayPos].tick > tick)
                    nextSp = chart.specialPhrases[arrayPos];

                if (nextSp != null)
                {
                    // Cap sustain length
                    if (nextSp.tick < tick)
                        newLength = 0;
                    else if (pos > nextSp.tick)
                        // Cap sustain
                        newLength = nextSp.tick - tick;
                }
                // else it's the last or only special phrase
            }

            return newLength;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonPhrase Clone()
        {
            return new MoonPhrase(tick, length, type);
        }

        public override string ToString()
        {
            return $"Special phrase at tick {tick} with type {type}, length {length}";
        }
    }
}
