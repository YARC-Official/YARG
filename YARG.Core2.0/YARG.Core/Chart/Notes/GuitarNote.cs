using System;

namespace YARG.Core.Chart
{
    public class GuitarNote : Note<GuitarNote>
    {
        private GuitarNoteFlags _guitarFlags;
        public GuitarNoteFlags GuitarFlags;

        public int Fret         { get; }
        public int DisjointMask { get; }
        public int NoteMask     { get; private set; }

        public GuitarNoteType Type { get; set; }

        public bool IsStrum => Type == GuitarNoteType.Strum;
        public bool IsHopo  => Type == GuitarNoteType.Hopo;
        public bool IsTap   => Type == GuitarNoteType.Tap;

        public bool IsSustain => TickLength > 0;

        public bool IsExtendedSustain => (GuitarFlags & GuitarNoteFlags.ExtendedSustain) != 0;
        public bool IsDisjoint        => (GuitarFlags & GuitarNoteFlags.Disjoint) != 0;

        public GuitarNote(FiveFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int) fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength)
        {
        }

        public GuitarNote(SixFretGuitarFret fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags,
            NoteFlags flags, double time, double timeLength, uint tick, uint tickLength)
            : this((int) fret, noteType, guitarFlags, flags, time, timeLength, tick, tickLength)
        {
        }

        public GuitarNote(int fret, GuitarNoteType noteType, GuitarNoteFlags guitarFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Fret = fret;
            Type = noteType;

            GuitarFlags = _guitarFlags = guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public GuitarNote(GuitarNote other) : base(other)
        {
            Fret = other.Fret;
            Type = other.Type;

            GuitarFlags = _guitarFlags = other._guitarFlags;

            NoteMask = GetNoteMask(Fret);
            DisjointMask = GetNoteMask(Fret);
        }

        public override void AddChildNote(GuitarNote note)
        {
            if ((NoteMask & GetNoteMask(note.Fret)) != 0) return;

            base.AddChildNote(note);

            NoteMask |= GetNoteMask(note.Fret);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            GuitarFlags = _guitarFlags;
        }

        protected override void CopyFlags(GuitarNote other)
        {
            _guitarFlags = other._guitarFlags;
            GuitarFlags = other.GuitarFlags;

            Type = other.Type;
        }

        protected override GuitarNote CloneNote()
        {
            return new(this);
        }
    }

    public enum FiveFretGuitarFret
    {
        Green = 1,
        Red,
        Yellow,
        Blue,
        Orange,
        Open = 7,
    }

    public enum SixFretGuitarFret
    {
        Black1 = 1,
        Black2,
        Black3,
        White1,
        White2,
        White3,
        Open,
    }

    public enum GuitarNoteType
    {
        Strum,
        Hopo,
        Tap
    }

    [Flags]
    public enum GuitarNoteFlags
    {
        None = 0,

        ExtendedSustain = 1 << 0,
        Disjoint        = 1 << 1,
    }
}