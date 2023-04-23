// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class Event : SongObject
    {
        private readonly ID _classID = ID.Event;

        public override int classID { get { return (int)_classID; } }

        public string title { get; private set; }

        public Event(string _title, uint _position) : base(_position)
        {
            title = _title;
        }

        public Event(Event songEvent) : base(songEvent.tick)
        {
            CopyFrom(songEvent);
        }

        public void CopyFrom(Event songEvent)
        {
            tick = songEvent.tick;
            title = songEvent.title;
        }

        public override SongObject Clone()
        {
            return new Event(this);
        }

        public override bool AllValuesCompare<T>(T songObject)
        {
            if (this == songObject && (songObject as Event).title == title)
                return true;
            else
                return false;
        }

        protected override bool Equals(SongObject b)
        {
            if (base.Equals(b))
            {
                Event realB = b as Event;
                return realB != null && tick == realB.tick && title == realB.title;
            }

            return false;
        }

        protected override bool LessThan(SongObject b)
        {
            if (this.classID == b.classID)
            {
                Event realB = b as Event;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (string.Compare(title, realB.title) < 0)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }
    }
}
