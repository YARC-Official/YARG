// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonText : MoonObject
    {
        public string text;

        public MoonText(string _title, uint _position)
            : this(ID.Text, _title, _position)
        {
        }

        protected MoonText(ID id, string _title, uint _position)
            : base(id, _position)
        {
            text = _title;
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonText textEv)
                return baseEq;

            return text == textEv.text;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonText textEv)
                return baseComp;

            return string.Compare(text, textEv.text);
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonText Clone()
        {
            return new MoonText(text, tick);
        }

        public override string ToString()
        {
            return $"Text event '{text}' at tick {tick}";
        }
    }
}
