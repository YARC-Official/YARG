using System.Collections;
using System.Collections.Generic;
using YARG.Core.Chart;

namespace YARG.Core.Engine
{
    public class SustainList<TNoteType> : IEnumerable<ActiveSustain<TNoteType>> where TNoteType : Note<TNoteType>
    {
        public struct SustainEnumerator : IEnumerator<ActiveSustain<TNoteType>>
        {
            private readonly SustainList<TNoteType> _list;

            private int _index;

            private ActiveSustain<TNoteType>? _current;

            public SustainEnumerator(SustainList<TNoteType> list)
            {
                _list = list;
                _index = 0;

                _current = default;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                if (_index < _list.Count)
                {
                    _current = _list[_index];
                    _index++;
                    return true;
                }

                _index = _list.Count;
                _current = default;
                return false;
            }

            public ActiveSustain<TNoteType> Current => _current!.Value;

            object IEnumerator.Current => Current;

            public void Reset()
            {
                _index = 0;
                _current = default;
            }

        }

        public ref ActiveSustain<TNoteType> this[int index] => ref _items[index];

        private ActiveSustain<TNoteType>[] _items;

        public int Count { get; private set; }

        public SustainList() : this(0)
        {
        }

        public SustainList(int capacity)
        {
            _items = new ActiveSustain<TNoteType>[capacity];
        }

        public void Add(ActiveSustain<TNoteType> sustain)
        {
            // Resize the array if necessary
            if (Count == _items.Length)
            {
                var newItems = new ActiveSustain<TNoteType>[_items.Length * 2];
                _items.CopyTo(newItems, 0);
                _items = newItems;
            }

            _items[Count++] = sustain;
        }

        public void RemoveAt(int index)
        {
            for (int i = index; i < Count - 1; i++)
            {
                _items[i] = _items[i + 1];
            }

            Count--;
        }

        public void Clear()
        {
            Count = 0;
        }

        public SustainEnumerator GetEnumerator() => new(this);

        IEnumerator<ActiveSustain<TNoteType>> IEnumerable<ActiveSustain<TNoteType>>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<ActiveSustain<TNoteType>>)this).GetEnumerator();
    }

    public struct ActiveSustain<TNoteType> where TNoteType : Note<TNoteType>
    {
        public TNoteType Note;
        public uint      BaseTick;
        public double    BaseScore;

        public bool HasFinishedScoring;
        public bool IsLeniencyHeld;

        public double LeniencyDropTime;

        public ActiveSustain(TNoteType note)
        {
            Note = note;
            BaseTick = note.Tick;
            BaseScore = 0;

            HasFinishedScoring = false;
            IsLeniencyHeld = false;

            LeniencyDropTime = -9999;
        }

        public double GetEndTime(SyncTrack syncTrack, uint sustainBurstThreshold)
        {
            // Sustain is too short for a burst, so clamp the burst to the start position of the note
            if (sustainBurstThreshold > Note.TickLength)
            {
                return Note.Time;
            }

            return syncTrack.TickToTime(Note.TickEnd - sustainBurstThreshold);
        }
    }
}