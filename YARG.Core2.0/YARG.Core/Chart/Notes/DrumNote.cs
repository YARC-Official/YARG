using System;

namespace YARG.Core.Chart
{
    public class DrumNote : Note<DrumNote>
    {
        private DrumNoteFlags _drumFlags;
        public DrumNoteFlags DrumFlags;

        public int Pad { get; }

        public DrumNoteType Type { get; set; }

        private int _padMask;

        public bool IsNeutral => Type == DrumNoteType.Neutral;
        public bool IsAccent  => Type == DrumNoteType.Accent;
        public bool IsGhost   => Type == DrumNoteType.Ghost;

        public float? HitVelocity;

        public bool IsStarPowerActivator => (DrumFlags & DrumNoteFlags.StarPowerActivator) != 0;

        public DrumNote(FourLaneDrumPad pad, DrumNoteType noteType, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, noteType, drumFlags, flags, time, tick)
        {
        }

        public DrumNote(FiveLaneDrumPad pad, DrumNoteType noteType, DrumNoteFlags drumFlags,
            NoteFlags flags, double time, uint tick)
            : this((int)pad, noteType, drumFlags, flags, time, tick)
        {
        }

        public DrumNote(int pad, DrumNoteType noteType, DrumNoteFlags drumFlags, NoteFlags flags, double time, uint tick)
            : base(flags, time, 0, tick, 0)
        {
            Pad = pad;
            Type = noteType;

            DrumFlags = _drumFlags = drumFlags;

            _padMask = 1 << pad;
        }

        public DrumNote(DrumNote other) : base(other)
        {
            Pad = other.Pad;
            Type = other.Type;

            DrumFlags = _drumFlags = other._drumFlags;

            _padMask = 1 << other.Pad;
        }

        public override void AddChildNote(DrumNote note)
        {
            if ((_padMask & (1 << note.Pad)) != 0) return;

            _padMask |= 1 << note.Pad;

            base.AddChildNote(note);
        }

        public override void ResetNoteState()
        {
            base.ResetNoteState();
            DrumFlags = _drumFlags;
            HitVelocity = null;
        }

        public void ActivateFlag(DrumNoteFlags drumNoteFlag)
        {
            _drumFlags |= drumNoteFlag;
            DrumFlags |= drumNoteFlag;
        }

        protected override void CopyFlags(DrumNote other)
        {
            _drumFlags = other._drumFlags;
            DrumFlags = other.DrumFlags;

            Type = other.Type;
        }

        protected override DrumNote CloneNote()
        {
            return new(this);
        }
    }

    public enum FourLaneDrumPad
    {
        Kick,

        RedDrum,
        YellowDrum,
        BlueDrum,
        GreenDrum,

        YellowCymbal,
        BlueCymbal,
        GreenCymbal,
    }

    public enum FiveLaneDrumPad
    {
        Kick,

        Red,
        Yellow,
        Blue,
        Orange,
        Green,
    }

    public enum DrumNoteType
    {
        Neutral,
        Ghost,
        Accent,
    }

    [Flags]
    public enum DrumNoteFlags
    {
        None = 0,

        StarPowerActivator = 1 << 0,
    }
}