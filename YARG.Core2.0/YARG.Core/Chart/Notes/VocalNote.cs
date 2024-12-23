using System;

namespace YARG.Core.Chart
{
    /// <summary>
    /// A note on a vocals track.
    /// </summary>
    public class VocalNote : Note<VocalNote>
    {
        /// <summary>
        /// The type of vocals note (either a phrase, a lyrical note or a percussion hit).
        /// </summary>
        public VocalNoteType Type { get; private set; }

        /// <summary>
        /// 0-based index for the harmony part this note is a part of.
        /// HARM1 is 0, HARM2 is 1, HARM3 is 2.
        /// </summary>
        public int HarmonyPart { get; }

        /// <summary>
        /// The MIDI pitch of the note, as a float.
        /// -1 means the note is unpitched.
        /// </summary>
        public float Pitch { get; }

        /// <summary>
        /// The octave of the vocal pitch.
        /// Octaves start at -1 in MIDI: note 60 is C4, note 12 is C0, note 0 is C-1.
        /// </summary>
        public int Octave => (int) (Pitch / 12) - 1;

        /// <summary>
        /// The pitch of the note wrapped relative to an octave (0-11).
        /// C is 0, B is 11. -1 means the note is unpitched.
        /// </summary>
        public float OctavePitch => Pitch % 12;

        /// <summary>
        /// The length of this note and all of its children, in seconds.
        /// </summary>
        public double TotalTimeLength { get; private set; }

        /// <summary>
        /// The time-based end of this note and all of its children.
        /// </summary>
        public double TotalTimeEnd => Time + TotalTimeLength;

        /// <summary>
        /// The length of this note and all of its children, in ticks.
        /// </summary>
        public uint TotalTickLength { get; private set; }
        /// <summary>
        /// The tick-based end of this note and all of its children.
        /// </summary>
        public uint TotalTickEnd => Tick + TotalTickLength;

        /// <summary>
        /// Whether or not this note is non-pitched.
        /// </summary>
        public bool IsNonPitched => Pitch < 0;

        /// <summary>
        /// Whether or not this note is a percussion note.
        /// </summary>
        public bool IsPercussion => Type == VocalNoteType.Percussion;

        /// <summary>
        /// Whether or not this note is a vocal phrase.
        /// </summary>
        public bool IsPhrase => Type == VocalNoteType.Phrase;

        /// <summary>
        /// Creates a new <see cref="VocalNote"/> with the given properties.
        /// This constructor should be used for notes only.
        /// </summary>
        public VocalNote(float pitch, int harmonyPart, VocalNoteType type,
            double time, double timeLength, uint tick, uint tickLength)
            : base(NoteFlags.None, time, timeLength, tick, tickLength)
        {
            Type = type;
            Pitch = pitch;
            HarmonyPart = harmonyPart;

            TotalTimeLength = timeLength;
            TotalTickLength = tickLength;
        }

        /// <summary>
        /// Creates a new <see cref="VocalNote"/> phrase with the given properties.
        /// This constructor should be used for vocal phrases only.
        /// </summary>
        public VocalNote(NoteFlags noteFlags,
            double time, double timeLength, uint tick, uint tickLength)
            : base(noteFlags, time, timeLength, tick, tickLength)
        {
            Type = VocalNoteType.Phrase;

            TotalTimeLength = timeLength;
            TotalTickLength = tickLength;
        }

        public VocalNote(VocalNote other) : base(other)
        {
            Type = other.Type;
            Pitch = other.Pitch;
            HarmonyPart = other.HarmonyPart;

            TotalTimeLength = other.TotalTimeLength;
            TotalTickLength = other.TotalTickLength;
        }

        /// <summary>
        /// Gets the pitch of this note and its children at the specified time.
        /// Clamps to the start and end if the time is out of bounds.
        /// </summary>
        public float PitchAtSongTime(double time)
        {
            if (Type == VocalNoteType.Phrase)
            {
                return -1f;
            }

            // Clamp to start
            if (time < TimeEnd || ChildNotes.Count < 1)
            {
                return Pitch;
            }

            // Search child notes
            var firstNote = this;
            foreach (var secondNote in ChildNotes)
            {
                // Check note bounds
                if (time >= firstNote.Time && time < secondNote.TimeEnd)
                {
                    // Check if time is in a specific pitch
                    if (time < firstNote.TimeEnd)
                        return firstNote.Pitch;

                    if (time >= secondNote.Time)
                        return secondNote.Pitch;

                    // Time is between the two pitches, lerp them
                    double percent = YargMath.InverseLerpD(firstNote.TimeEnd, secondNote.Time, time);
                    return YargMath.Lerp(firstNote.Pitch, secondNote.Pitch, percent);
                }

                firstNote = secondNote;
            }

            // Clamp to end
            return ChildNotes[^1].Pitch;
        }

        public override void AddChildNote(VocalNote note)
        {
            /*
             TODO Add same child note checking like the other instruments
             (but I have no idea how vocals works) - Riley
            */

            if (IsPhrase)
            {
                if (note.Tick < Tick) return;

                _childNotes.Add(note);

                // Sort child notes by tick
                _childNotes.Sort((note1, note2) =>
                {
                    if (note1.Tick > note2.Tick) return 1;
                    if (note1.Tick < note2.Tick) return -1;
                    return 0;
                });
            }
            else
            {
                if (note.Tick <= Tick || note.ChildNotes.Count > 0) return;

                _childNotes.Add(note);

                // Sort child notes by tick
                _childNotes.Sort((note1, note2) =>
                {
                    if (note1.Tick > note2.Tick) return 1;
                    if (note1.Tick < note2.Tick) return -1;
                    return 0;
                });

                // Track total length
                TotalTimeLength = _childNotes[^1].TimeEnd - Time;
                TotalTickLength = _childNotes[^1].TickEnd - Tick;
            }
        }

        protected override void CopyFlags(VocalNote other)
        {
            Type = other.Type;
        }

        protected override VocalNote CloneNote()
        {
            return new(this);
        }
    }

    /// <summary>
    /// Possible vocal note types.
    /// </summary>
    public enum VocalNoteType
    {
        Phrase,
        Lyric,
        Percussion
    }
}