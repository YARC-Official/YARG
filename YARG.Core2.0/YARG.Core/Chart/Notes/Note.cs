using System;
using System.Collections.Generic;
using System.Linq;

namespace YARG.Core.Chart
{
    [Flags]
    public enum NoteFlags
    {
        None = 0,

        StarPower      = 1 << 0,
        StarPowerStart = 1 << 1,
        StarPowerEnd   = 1 << 2,

        Solo      = 1 << 3,
        SoloStart = 1 << 4,
        SoloEnd   = 1 << 5,
    }

    public abstract class Note<TNote> : ChartEvent, ICloneable<TNote>
        where TNote : Note<TNote>
    {
        public struct AllNotesEnumerator
        {
            private readonly TNote _note;
            private int _index;

            public AllNotesEnumerator(TNote note)
            {
                _note = note;
                _index = -1;
            }

            public TNote Current => _index == 0
                ? _note
                : _note.ChildNotes[_index - 1];

            public bool MoveNext()
            {
                _index++;
                return _index <= _note.ChildNotes.Count;
            }

            public void Reset()
            {
                _index = -1;
            }

            public AllNotesEnumerator GetEnumerator()
            {
                return this;
            }
        }

        protected readonly List<TNote> _childNotes = new();

        private NoteFlags _flags;
        public  NoteFlags Flags;

        private TNote? _originalPreviousNote;
        private TNote? _originalNextNote;

        public TNote? PreviousNote;
        public TNote? NextNote;

        public uint SustainTicksHeld;

        public TNote? Parent { get; private set; }
        public IReadOnlyList<TNote> ChildNotes => _childNotes;

        /// <summary>
        /// Returns this note's parent, and if it's null, returns itself instead.
        /// </summary>
        public TNote ParentOrSelf => Parent ?? (TNote) this;
        public bool  IsChord      => _childNotes.Count > 0;

        public bool IsParent => Parent == null;
        public bool IsChild  => !IsParent;

        public bool IsStarPower      => (Flags & NoteFlags.StarPower) != 0;
        public bool IsStarPowerStart => (Flags & NoteFlags.StarPowerStart) != 0;
        public bool IsStarPowerEnd   => (Flags & NoteFlags.StarPowerEnd) != 0;

        public bool IsSolo => (Flags & NoteFlags.Solo) != 0;
        public bool IsSoloStart => (Flags & NoteFlags.SoloStart) != 0;
        public bool IsSoloEnd   => (Flags & NoteFlags.SoloEnd) != 0;

        /// <summary>
        /// Returns an enumerator that contains all child notes and the parent note itself (allocation free).
        /// </summary>
        public AllNotesEnumerator AllNotes => new((TNote) this);

        public bool WasHit;
        public bool WasMissed;

        protected Note(NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : base(time, timeLength, tick, tickLength)
        {
            Flags = _flags = flags;
        }

        protected Note(Note<TNote> other) : base(other)
        {
            Flags = _flags = other._flags;

            // Child notes are not added here, since this is called before the inheritor's constructor is
        }

        public virtual void AddChildNote(TNote note)
        {
            if (note.Tick != Tick)
                throw new InvalidOperationException("Child note being added is not on the same tick!");
            if (note.ChildNotes.Count > 0)
                throw new InvalidOperationException("Child note being added has its own children!");

            note.Parent = (TNote) this;
            _childNotes.Add(note);
        }

        public void SetHitState(bool hit, bool includeChildren)
        {
            WasHit = hit;
            if (!includeChildren) return;

            foreach (var childNote in _childNotes)
            {
                childNote.SetHitState(hit, true);
            }
        }

        public void SetMissState(bool miss, bool includeChildren)
        {
            WasMissed = miss;
            if (!includeChildren) return;

            foreach (var childNote in _childNotes)
            {
                childNote.SetMissState(miss, true);
            }
        }

        public bool WasFullyHit()
        {
            if (!WasHit)
            {
                return false;
            }

            foreach (var childNote in _childNotes)
            {
                if (!childNote.WasFullyHit())
                {
                    return false;
                }
            }

            return true;
        }

        public bool WasFullyMissed()
        {
            if (!WasMissed)
            {
                return false;
            }

            foreach (var childNote in _childNotes)
            {
                if (!childNote.WasFullyMissed())
                {
                    return false;
                }
            }

            return true;
        }

        public bool WasFullyHitOrMissed()
        {
            if (!WasMissed && !WasHit)
            {
                return false;
            }

            foreach (var childNote in _childNotes)
            {
                if (!childNote.WasFullyHitOrMissed())
                {
                    return false;
                }
            }

            return true;
        }

        public void OverridePreviousNote()
        {
            // Prevent overriding previous note more than once without resetting note state
            if(_originalPreviousNote != null)
            {
                throw new InvalidOperationException("Cannot override previous note more than once");
            }

            _originalPreviousNote = PreviousNote;
            PreviousNote = null;
        }

        public void OverrideNextNote()
        {
            // Prevent overriding next note more than once without resetting note state
            if(_originalNextNote != null)
            {
                throw new InvalidOperationException("Cannot override next note more than once");
            }

            _originalNextNote = NextNote;
            NextNote = null;
        }

        public virtual void ResetNoteState()
        {
            Flags = _flags;
            WasHit = false;
            WasMissed = false;
            SustainTicksHeld = 0;

            if(_originalPreviousNote != null)
            {
                PreviousNote = _originalPreviousNote;
            }
            if (_originalNextNote != null)
            {
                NextNote = _originalNextNote;
            }

            _originalPreviousNote = null;
            _originalNextNote = null;

            foreach(var childNote in _childNotes)
            {
                childNote.ResetNoteState();
            }
        }

        protected static int GetNoteMask(int note)
        {
            // Resulting shift is 1 too high, shifting down by 1 corrects this.
            // Reason for not doing (note - 1) is this breaks open notes. (1 << (0 - 1) == 0x80000000)
            // Shifting down by 1 accounts for open notes and sets the mask to 0.
            int mask = 1 << note;
            mask >>= 1;
            return mask;
        }

        public void CopyValuesFrom(TNote other)
        {
            Time = other.Time;
            TimeLength = other.TimeLength;
            Tick = other.Tick;
            TickLength = other.TickLength;

            _flags = other._flags;
            Flags = other.Flags;

            CopyFlags(other);
        }

        public void ActivateFlag(NoteFlags noteFlag)
        {
            _flags |= noteFlag;
            Flags |= noteFlag;
        }

        public void ResetFlags()
        {
            Flags = _flags;
        }

        protected abstract void CopyFlags(TNote other);
        protected abstract TNote CloneNote();

        /// <summary>
        /// Creates a copy of this note with the same set of values.
        /// </summary>
        /// <remarks>
        /// NOTE: Next/previous references and changes in state are not preserved,
        /// notes are re-created from scratch.
        /// </remarks>
        public TNote Clone()
        {
            var newNote = CloneWithoutChildNotes();
            foreach (var child in _childNotes)
            {
                newNote.AddChildNote(child.Clone());
            }

            return newNote;
        }

        /// <summary>
        /// Creates a copy of this note with the same set of values, but without the child notes
        /// </summary>
        /// <remarks>
        /// NOTE: Next/previous references and changes in state are not preserved,
        /// notes are re-created from scratch.
        /// </remarks>
        public TNote CloneWithoutChildNotes()
        {
            var newNote = CloneNote();
            return newNote;
        }
    }
}