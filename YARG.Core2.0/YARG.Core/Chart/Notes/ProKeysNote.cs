using System;

namespace YARG.Core.Chart
{
    public class ProKeysNote : Note<ProKeysNote>
    {
        private ProKeysNoteFlags _proKeysFlags;
        public ProKeysNoteFlags ProKeysFlags;

        public int Key          { get; }
        public int DisjointMask { get; }
        public int NoteMask     { get; private set; }

        public bool IsGlissando => (ProKeysFlags & ProKeysNoteFlags.Glissando) != 0;

        public bool IsSustain => TickLength > 0;

        public ProKeysNote(int key, ProKeysNoteFlags proKeysFlags, NoteFlags flags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(flags, time, timeLength, tick, tickLength)
        {
            Key = key;

            ProKeysFlags = _proKeysFlags = proKeysFlags;

            NoteMask = GetKeyMask(Key);
        }

        public ProKeysNote(ProKeysNote other) : base(other)
        {
            Key = other.Key;

            ProKeysFlags = _proKeysFlags = other._proKeysFlags;

            NoteMask = GetKeyMask(Key);
            DisjointMask = GetKeyMask(Key);
        }

        public override void AddChildNote(ProKeysNote note)
        {
            if ((NoteMask & GetKeyMask(note.Key)) != 0) return;

            base.AddChildNote(note);

            NoteMask |= GetKeyMask(note.Key);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            ProKeysFlags = _proKeysFlags;
        }

        protected override void CopyFlags(ProKeysNote other)
        {
            _proKeysFlags = other._proKeysFlags;
            ProKeysFlags = other.ProKeysFlags;
        }

        protected override ProKeysNote CloneNote()
        {
            return new(this);
        }

        private static int GetKeyMask(int key)
        {
            return 1 << key;
        }
    }

    [Flags]
    public enum ProKeysNoteFlags
    {
        None = 0,

        Glissando = 1 << 0,
    }
}