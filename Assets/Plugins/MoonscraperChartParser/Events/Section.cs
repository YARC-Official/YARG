// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class Section : Event
    {
        private readonly ID _classID = ID.Section;

        public override int classID { get { return (int)_classID; } }

        public Section(string _title, uint _position) : base(_title, _position) { }

        public Section(Section section) : base(section.title, section.tick) { }

        public override SongObject Clone()
        {
            return new Section(this);
        }
    }
}
