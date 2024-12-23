// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal abstract class MoonObject
    {
        /// <summary>
        /// The tick position of the object
        /// </summary>
        public uint tick;

        public readonly ID classID;
        
        public MoonObject(ID id, uint _tick)
        {
            classID = id;
            tick = _tick;
        }

        // Clone needs to be hideable so it can return a different type in derived classes
        protected abstract MoonObject CloneImpl();
        public MoonObject Clone() => CloneImpl();

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || (obj is MoonObject songObj && ValueEquals(songObj));
        }

        public virtual bool ValueEquals(MoonObject obj)
        {
            return tick == obj.tick && classID == obj.classID;
        }

        public bool InsertionEquals(MoonObject obj)
        {
            return InsertionCompareTo(obj) == 0;
        }

        public virtual int InsertionCompareTo(MoonObject obj)
        {
            int tickComp = tick.CompareTo(obj.tick);
            if (tickComp != 0)
                return tickComp;

            int idComp = ((int)classID).CompareTo((int)obj.classID);
            if (idComp != 0)
                return idComp;

            return 0;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return $"{classID} at tick {tick}";
        }

        /// <summary>
        /// Allows different classes to be sorted and grouped together in arrays by giving each class a comparable numeric value that is greater or less than other classes.
        /// </summary>
        public enum ID
        {
            TimeSignature,
            BPM,
            Anchor,
            Beat,
            Text,
            Venue,
            Note,
            Phrase,
        }
    }
}
