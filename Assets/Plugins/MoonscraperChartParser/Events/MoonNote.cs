// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    [System.Serializable]
    public class MoonNote : ChartObject
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

        public enum MoonNoteType
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
        public MoonNote previous;
        /// <summary>
        /// The next note in the linked-list.
        /// </summary>
        public MoonNote next;

        public Chord chord { get { return new Chord(this); } }
#if APPLICATION_MOONSCRAPER
        new public NoteController controller
        {
            get { return (NoteController)base.controller; }
            set { base.controller = value; }
        }
#endif 
        public MoonNote(uint _position,
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

        public MoonNote(uint _position,
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

        public MoonNote(MoonNote moonNote) : base(moonNote.tick)
        {
            tick = moonNote.tick;
            length = moonNote.length;
            flags = moonNote.flags;
            rawNote = moonNote.rawNote;
        }

        public void CopyFrom(MoonNote moonNote)
        {
            tick = moonNote.tick;
            length = moonNote.length;
            flags = moonNote.flags;
            rawNote = moonNote.rawNote;
        }

        public MoonChart.GameMode gameMode
        {
            get
            {
                if (moonChart != null)
                    return moonChart.gameMode;
                else
                {
#if APPLICATION_MOONSCRAPER     // Moonscraper doesn't use note.chart.gameMode directly because notes might not have charts associated with them, esp when copy-pasting and storing undo-redo
                    return ChartEditor.Instance.currentChart.gameMode;
#else
                return MoonChart.GameMode.Unrecognised;
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
        public MoonNote NextSeperateMoonNote
        {
            get
            {
                MoonNote nextMoonNote = next;
                while (nextMoonNote != null && nextMoonNote.tick == tick)
                    nextMoonNote = nextMoonNote.next;
                return nextMoonNote;
            }
        }

        /// <summary>
        /// Gets the previous note in the linked-list that's not part of this note's chord.
        /// </summary>
        public MoonNote PreviousSeperateMoonNote
        {
            get
            {
                MoonNote previousMoonNote = previous;
                while (previousMoonNote != null && previousMoonNote.tick == tick)
                    previousMoonNote = previousMoonNote.previous;
                return previousMoonNote;
            }
        }

        public override SongObject Clone()
        {
            return new MoonNote(this);
        }

        public override bool AllValuesCompare<T>(T songObject)
        {
            if (this == songObject && (songObject as MoonNote).length == length && (songObject as MoonNote).rawNote == rawNote && (songObject as MoonNote).flags == flags)
                return true;
            else
                return false;
        }

        protected override bool Equals(SongObject b)
        {
            if (b.GetType() == typeof(MoonNote))
            {
                MoonNote realB = b as MoonNote;
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
            if (b.GetType() == typeof(MoonNote))
            {
                MoonNote realB = b as MoonNote;
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
                        int HOPODistance = (int)(SongConfig.FORCED_NOTE_TICK_THRESHOLD * moonSong.resolution / SongConfig.STANDARD_BEAT_RESOLUTION);

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

                MoonNote moonNote = this;
                while (moonNote.previous != null && moonNote.previous.tick == tick)
                {
                    moonNote = moonNote.previous;
                    mask |= (1 << moonNote.rawNote);
                }

                moonNote = this;
                while (moonNote.next != null && moonNote.tick == moonNote.next.tick)
                {
                    moonNote = moonNote.next;
                    mask |= (1 << moonNote.rawNote);
                }

                return mask;
            }
        }

        public int GetMaskWithRequiredFlags(Flags flags)
        {
            int mask = 0;

            foreach (MoonNote note in this.chord)
            {
                if (note.flags == flags)
                    mask |= (1 << note.rawNote);
            }

            return mask;
        }

        /// <summary>
        /// Live calculation of what Note_Type this note would currently be. 
        /// </summary>
        public MoonNoteType type
        {
            get
            {
                if (this.gameMode == MoonChart.GameMode.Drums)
                {
                    if (!this.IsOpenNote() && (flags & Flags.ProDrums_Cymbal) == Flags.ProDrums_Cymbal)
                    {
                        return MoonNoteType.Cymbal;
                    }

                    return MoonNoteType.Strum;
                }
                else
                {
                    if (!this.IsOpenNote() && (flags & Flags.Tap) == Flags.Tap)
                    {
                        return MoonNoteType.Tap;
                    }
                    else
                    {
                        if (isHopo)
                            return MoonNoteType.Hopo;
                        else
                            return MoonNoteType.Strum;
                    }
                }
            }
        }

        public bool cannotBeForced
        {
            get
            {
                MoonNote seperatePrevious = PreviousSeperateMoonNote;

                if ((seperatePrevious == null) || (seperatePrevious != null && mask == seperatePrevious.mask))
                    return true;

                return false;
            }
        }

        public class Chord : IEnumerable<MoonNote>
        {
            MoonNote _baseMoonNote;
            public Chord(MoonNote moonNote) : base()
            {
                _baseMoonNote = moonNote;
            }

            public IEnumerator<MoonNote> GetEnumerator()
            {
                MoonNote moonNote = _baseMoonNote;

                while (moonNote.previous != null && moonNote.previous.tick == moonNote.tick)
                {
                    moonNote = moonNote.previous;
                }

                yield return moonNote;

                while (moonNote.next != null && moonNote.tick == moonNote.next.tick)
                {
                    moonNote = moonNote.next;
                    yield return moonNote;
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
        }

        public delegate void ChordEnumerateFn(MoonNote moonNote);
        public void EnumerateChord(ChordEnumerateFn fn)
        {
            MoonNote moonNote = this;
            while (moonNote.previous != null && moonNote.previous.tick == moonNote.tick)
            {
                moonNote = moonNote.previous;
            }

            fn(moonNote);

            while (moonNote.next != null && moonNote.tick == moonNote.next.tick)
            {
                moonNote = moonNote.next;
                fn(moonNote);
            }
        }

        public bool IsOpenNote()
        {
            if (gameMode == MoonChart.GameMode.GHLGuitar)
                return ghliveGuitarFret == GHLiveGuitarFret.Open;
            else
                return guitarFret == GuitarFret.Open;
        }

        public static Flags GetBannedFlagsForGameMode(MoonChart.GameMode gameMode)
        {
            Flags bannedFlags = Flags.None;

            switch (gameMode)
            {
                case MoonChart.GameMode.Guitar:
                case MoonChart.GameMode.GHLGuitar:
                    {
                        bannedFlags = Flags.ProDrums_Cymbal | Flags.ProDrums_Accent | Flags.ProDrums_Ghost;
                        break;
                    }
                case MoonChart.GameMode.Drums:
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
