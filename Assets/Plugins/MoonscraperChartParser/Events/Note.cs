// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class Note : ChartObject
    {
        public enum GuitarFret
        {
            // Assign to the sprite array position
            Green = 0,
            Red = 1,
            Yellow = 2,
            Blue = 3,
            Orange = 4,
            Open = 5
        }

        public enum DrumPad
        {
            // Wrapper to account for how the frets change colours between the drums and guitar tracks from the GH series  
            Red = GuitarFret.Green,
            Yellow = GuitarFret.Red,
            Blue = GuitarFret.Yellow,
            Orange = GuitarFret.Blue,
            Green = GuitarFret.Orange,
            Kick = GuitarFret.Open,
        }

        public enum GHLiveGuitarFret
        {
            // Assign to the sprite array position
            Black1,
            Black2,
            Black3,
            White1,
            White2,
            White3,
            Open
        }

        public enum NoteType
        {
            Natural,
            Strum,
            Hopo,
            Tap,
            Cymbal,
        }

        public enum SpecialType
        {
            None,
            StarPower,
            Battle
        }

        [Flags]
        public enum Flags
        {
            None = 0,

            // Guitar
            Forced = 1 << 0,
            Tap = 1 << 1,

            // RB Pro Drums
            ProDrums_Cymbal = 1 << 6,

            // Generic flag that mainly represents mechanics from Guitar Hero's Expert+ filtered drum notes such as Double Kick. This may apply to any difficulty now though.
            InstrumentPlus = 1 << 7,
            DoubleKick = InstrumentPlus,

            // FoF/PS Pro Drums
            ProDrums_Accent = 1 << 12,
            ProDrums_Ghost = 1 << 13,
        }

        public const Flags PER_NOTE_FLAGS = Flags.ProDrums_Cymbal | Flags.InstrumentPlus | Flags.ProDrums_Accent | Flags.ProDrums_Ghost;

        private readonly ID _classID = ID.Note;

        public override int classID { get { return (int)_classID; } }

        public uint length;
        public int rawNote;
        public GuitarFret guitarFret
        {
            get
            {
                return (GuitarFret)rawNote;
            }
            set
            {
                rawNote = (int)value;
            }
        }

        public DrumPad drumPad
        {
            get
            {
                return (DrumPad)guitarFret;
            }
        }

        public GHLiveGuitarFret ghliveGuitarFret
        {
            get
            {
                return (GHLiveGuitarFret)rawNote;
            }
            set
            {
                rawNote = (int)value;
            }
        }

        /// <summary>
        /// Properties, such as forced or taps, are stored here in a bitwise format.
        /// </summary>
        public Flags flags;

        /// <summary>
        /// The previous note in the linked-list.
        /// </summary>
        public Note previous;
        /// <summary>
        /// The next note in the linked-list.
        /// </summary>
        public Note next;

        public Chord chord { get { return new Chord(this); } }
#if APPLICATION_MOONSCRAPER
        new public NoteController controller
        {
            get { return (NoteController)base.controller; }
            set { base.controller = value; }
        }
#endif 
        public Note(uint _position,
                    int _rawNote,
                    uint _sustain = 0,
                    Flags _flags = Flags.None) : base(_position)
        {
            length = _sustain;
            flags = _flags;
            rawNote = _rawNote;

            previous = null;
            next = null;
        }

        public Note(uint _position,
                    GuitarFret _fret_type,
                    uint _sustain = 0,
                    Flags _flags = Flags.None) : base(_position)
        {
            length = _sustain;
            flags = _flags;
            guitarFret = _fret_type;

            previous = null;
            next = null;
        }

        public Note(Note note) : base(note.tick)
        {
            tick = note.tick;
            length = note.length;
            flags = note.flags;
            rawNote = note.rawNote;
        }

        public void CopyFrom(Note note)
        {
            tick = note.tick;
            length = note.length;
            flags = note.flags;
            rawNote = note.rawNote;
        }

        public Chart.GameMode gameMode
        {
            get
            {
                if (chart != null)
                    return chart.gameMode;
                else
                {
#if APPLICATION_MOONSCRAPER     // Moonscraper doesn't use note.chart.gameMode directly because notes might not have charts associated with them, esp when copy-pasting and storing undo-redo
                    return ChartEditor.Instance.currentChart.gameMode;
#else
                return Chart.GameMode.Unrecognised;
#endif
                }
            }
        }

        public bool forced
        {
            get
            {
                return (flags & Flags.Forced) == Flags.Forced;
            }
            set
            {
                if (value)
                    flags = flags | Flags.Forced;
                else
                    flags = flags & ~Flags.Forced;
            }
        }

        /// <summary>
        /// Gets the next note in the linked-list that's not part of this note's chord.
        /// </summary>
        public Note nextSeperateNote
        {
            get
            {
                Note nextNote = next;
                while (nextNote != null && nextNote.tick == tick)
                    nextNote = nextNote.next;
                return nextNote;
            }
        }

        /// <summary>
        /// Gets the previous note in the linked-list that's not part of this note's chord.
        /// </summary>
        public Note previousSeperateNote
        {
            get
            {
                Note previousNote = previous;
                while (previousNote != null && previousNote.tick == tick)
                    previousNote = previousNote.previous;
                return previousNote;
            }
        }

        public override SongObject Clone()
        {
            return new Note(this);
        }

        public override bool AllValuesCompare<T>(T songObject)
        {
            if (this == songObject && (songObject as Note).length == length && (songObject as Note).rawNote == rawNote && (songObject as Note).flags == flags)
                return true;
            else
                return false;
        }

        protected override bool Equals(SongObject b)
        {
            if (b.GetType() == typeof(Note))
            {
                Note realB = b as Note;
                if (tick == realB.tick && rawNote == realB.rawNote)
                    return true;
                else
                    return false;
            }
            else
                return base.Equals(b);
        }

        protected override bool LessThan(SongObject b)
        {
            if (b.GetType() == typeof(Note))
            {
                Note realB = b as Note;
                if (tick < b.tick)
                    return true;
                else if (tick == b.tick)
                {
                    if (rawNote < realB.rawNote)
                        return true;
                }

                return false;
            }
            else
                return base.LessThan(b);
        }

        public bool isChord
        {
            get
            {
                return ((previous != null && previous.tick == tick) || (next != null && next.tick == tick));
            }
        }

        /// <summary>
        /// Ignores the note's forced flag when determining whether it would be a hopo or not
        /// </summary>
        public bool isNaturalHopo
        {
            get
            {
                bool HOPO = false;

                if (!isChord && previous != null)
                {
                    bool prevIsChord = previous.isChord;
                    // Need to consider whether the previous note was a chord, and if they are the same type of note
                    if (prevIsChord || (!prevIsChord && rawNote != previous.rawNote))
                    {
                        // Check distance from previous note 
                        int HOPODistance = (int)(SongConfig.FORCED_NOTE_TICK_THRESHOLD * song.resolution / SongConfig.STANDARD_BEAT_RESOLUTION);

                        if (tick - previous.tick <= HOPODistance)
                            HOPO = true;
                    }
                }

                return HOPO;
            }
        }

        /// <summary>
        /// Would this note be a hopo or not? (Ignores whether the note's tap flag is set or not.)
        /// </summary>
        bool isHopo
        {
            get
            {
                bool HOPO = isNaturalHopo;

                // Check if forced
                if (forced)
                    HOPO = !HOPO;

                return HOPO;
            }
        }

        /// <summary>
        /// Returns a bit mask representing the whole note's chord. For example, a green, red and blue chord would have a mask of 0000 1011. A yellow and orange chord would have a mask of 0001 0100. 
        /// Shifting occurs accoring the values of the Fret_Type enum, so open notes currently output with a mask of 0010 0000.
        /// </summary>
        public int mask
        {
            get
            {
                // Don't interate using chord, as chord will get messed up for the tool notes which override their linked list references. 
                int mask = 1 << rawNote;

                Note note = this;
                while (note.previous != null && note.previous.tick == tick)
                {
                    note = note.previous;
                    mask |= (1 << note.rawNote);
                }

                note = this;
                while (note.next != null && note.tick == note.next.tick)
                {
                    note = note.next;
                    mask |= (1 << note.rawNote);
                }

                return mask;
            }
        }

        public int GetMaskWithRequiredFlags(Flags flags)
        {
            int mask = 0;

            foreach (Note note in this.chord)
            {
                if (note.flags == flags)
                    mask |= (1 << note.rawNote);
            }

            return mask;
        }

        /// <summary>
        /// Live calculation of what Note_Type this note would currently be. 
        /// </summary>
        public NoteType type
        {
            get
            {
                if (this.gameMode == Chart.GameMode.Drums)
                {
                    if (!this.IsOpenNote() && (flags & Flags.ProDrums_Cymbal) == Flags.ProDrums_Cymbal)
                    {
                        return NoteType.Cymbal;
                    }

                    return NoteType.Strum;
                }
                else
                {
                    if (!this.IsOpenNote() && (flags & Flags.Tap) == Flags.Tap)
                    {
                        return NoteType.Tap;
                    }
                    else
                    {
                        if (isHopo)
                            return NoteType.Hopo;
                        else
                            return NoteType.Strum;
                    }
                }
            }
        }

        public bool cannotBeForced
        {
            get
            {
                Note seperatePrevious = previousSeperateNote;

                if ((seperatePrevious == null) || (seperatePrevious != null && mask == seperatePrevious.mask))
                    return true;

                return false;
            }
        }

        public class Chord : IEnumerable<Note>
        {
            Note baseNote;
            public Chord(Note note) : base()
            {
                baseNote = note;
            }

            public IEnumerator<Note> GetEnumerator()
            {
                Note note = baseNote;

                while (note.previous != null && note.previous.tick == note.tick)
                {
                    note = note.previous;
                }

                yield return note;

                while (note.next != null && note.tick == note.next.tick)
                {
                    note = note.next;
                    yield return note;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public delegate void ChordEnumerateFn(Note note);
        public void EnumerateChord(ChordEnumerateFn fn)
        {
            Note note = this;
            while (note.previous != null && note.previous.tick == note.tick)
            {
                note = note.previous;
            }

            fn(note);

            while (note.next != null && note.tick == note.next.tick)
            {
                note = note.next;
                fn(note);
            }
        }

        public bool IsOpenNote()
        {
            if (gameMode == Chart.GameMode.GHLGuitar)
                return ghliveGuitarFret == GHLiveGuitarFret.Open;
            else
                return guitarFret == GuitarFret.Open;
        }

        public static Flags GetBannedFlagsForGameMode(Chart.GameMode gameMode)
        {
            Flags bannedFlags = Flags.None;

            switch (gameMode)
            {
                case Chart.GameMode.Guitar:
                case Chart.GameMode.GHLGuitar:
                    {
                        bannedFlags = Flags.ProDrums_Cymbal | Flags.ProDrums_Accent | Flags.ProDrums_Ghost;
                        break;
                    }
                case Chart.GameMode.Drums:
                    {
                        bannedFlags = Flags.Forced | Flags.Tap;
                        break;
                    }
                default:
                    {
                        break;
                    }
            }

            return bannedFlags;
        }
    }
}
