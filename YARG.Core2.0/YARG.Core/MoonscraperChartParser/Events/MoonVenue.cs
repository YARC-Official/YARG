// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using YARG.Core.Chart;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonVenue : MoonText
    {
        public VenueLookup.Type type;
        public uint length;

        public MoonVenue(VenueLookup.Type _type, string _text, uint _position, uint _length = 0)
            : base(ID.Venue, _text, _position)
        {
            type = _type;
            length = _length;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonVenue venueEv)
                return baseEq;

            return type == venueEv.type || length == venueEv.length;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonVenue venueEv)
                return baseComp;

            return ((int) type).CompareTo((int) venueEv.type);
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonVenue Clone()
        {
            return new MoonVenue(type, text, tick, length);
        }

        public override string ToString()
        {
            return $"Venue event '{text}' at tick {tick} with type {type}, length {length}";
        }
    }
}
