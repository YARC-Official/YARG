// Copyright (c) 2016-2020 Alexander Ong
// See LICENSE in project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;

namespace MoonscraperChartEditor.Song
{
    [Serializable]
    internal class MoonNote : MoonObject
    {
        public enum GuitarFret
        {
            Open,
            Green,
            Red,
            Yellow,
            Blue,
            Orange,
        }

        public enum DrumPad
        {
            Kick,
            Red,
            Yellow,
            Blue,
            Orange,
            Green,
        }

        public enum GHLiveGuitarFret
        {
            Open,
            Black1,
            Black2,
            Black3,
            White1,
            White2,
            White3,
        }

        public enum ProGuitarString
        {
            Red,
            Green,
            Orange,
            Blue,
            Yellow,
            Purple
        }

        private const int PRO_GUITAR_FRET_OFFSET = 0;
        private const int PRO_GUITAR_FRET_MASK = 0x1F << PRO_GUITAR_FRET_OFFSET;
        private const int PRO_GUITAR_STRING_OFFSET = 5;
        private const int PRO_GUITAR_STRING_MASK = 0x07 << PRO_GUITAR_STRING_OFFSET;

        public enum MoonNoteType
        {
            Natural,
            Strum,
            Hopo,
            Tap,
            Cymbal,
        }

        [Flags]
        // TODO: These need to be organized a little better down the line
        public enum Flags
        {
            None = 0,

            // Guitar
            Forced = 1 << 0,
            Forced_Strum = 1 << 1,
            Forced_Hopo = 1 << 2,
            Tap = 1 << 3,

            // Pro Drums
            ProDrums_Cymbal = 1 << 4,
            ProDrums_Accent = 1 << 5,
            ProDrums_Ghost = 1 << 6,

            // Generic flag that mainly represents mechanics from Guitar Hero's Expert+ filtered drum notes such as Double Kick. This may apply to any difficulty now though.
            InstrumentPlus = 1 << 7,
            DoubleKick = InstrumentPlus,

            // Pro Guitar
            ProGuitar_Muted = 1 << 8,

            // Vocals
            Vocals_Percussion = 1 << 9,
        }

        public uint length;
        public int rawNote;

        public GuitarFret guitarFret
        {
            get => (GuitarFret)rawNote;
            set => rawNote = (int)value;
        }

        public DrumPad drumPad
        {
            get => (DrumPad)rawNote;
            set => rawNote = (int)value;
        }

        public GHLiveGuitarFret ghliveGuitarFret
        {
            get => (GHLiveGuitarFret)rawNote;
            set => rawNote = (int)value;
        }

        public int proGuitarFret
        {
            get => (rawNote & PRO_GUITAR_FRET_MASK) >> PRO_GUITAR_FRET_OFFSET;
            set => rawNote = MakeProGuitarRawNote(proGuitarString, value);
        }

        public ProGuitarString proGuitarString
        {
            get => (ProGuitarString)((rawNote & PRO_GUITAR_STRING_MASK) >> PRO_GUITAR_STRING_OFFSET);
            set => rawNote = MakeProGuitarRawNote(value, proGuitarFret);
        }

        public int proKeysKey
        {
            get => rawNote;
            set => rawNote = Math.Clamp(value, 0, 24);
        }

        /// <summary>
        /// MIDI note of the vocals pitch, typically ranging from C2 (36) to C6 (84).
        /// </summary>
        public int vocalsPitch
        {
            get => rawNote;
            set => rawNote = Math.Clamp(value, 0, 127);
        }

        /// <summary>
        /// Properties, such as forced or taps, are stored here in a bitwise format.
        /// </summary>
        public Flags flags;

        /// <summary>
        /// The previous note in the linked-list.
        /// </summary>
        public MoonNote? previous;
        /// <summary>
        /// The next note in the linked-list.
        /// </summary>
        public MoonNote? next;

        public Chord chord => new(this);

        public MoonNote(uint _position, int _rawNote, uint _sustain = 0, Flags _flags = Flags.None)
            : base(ID.Note, _position)
        {
            length = _sustain;
            flags = _flags;
            rawNote = _rawNote;

            previous = null;
            next = null;
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
                    flags |= Flags.Forced;
                else
                    flags &= ~Flags.Forced;
            }
        }

        /// <summary>
        /// Gets the next note in the linked-list that's not part of this note's chord.
        /// </summary>
        public MoonNote? NextSeperateMoonNote
        {
            get
            {
                var nextMoonNote = next;
                while (nextMoonNote != null && nextMoonNote.tick == tick)
                    nextMoonNote = nextMoonNote.next;
                return nextMoonNote;
            }
        }

        /// <summary>
        /// Gets the previous note in the linked-list that's not part of this note's chord.
        /// </summary>
        public MoonNote? PreviousSeperateMoonNote
        {
            get
            {
                var previousMoonNote = previous;
                while (previousMoonNote != null && previousMoonNote.tick == tick)
                    previousMoonNote = previousMoonNote.previous;
                return previousMoonNote;
            }
        }

        public override bool ValueEquals(MoonObject obj)
        {
            bool baseEq = base.ValueEquals(obj);
            if (!baseEq || obj is not MoonNote note)
                return baseEq;

            return rawNote == note.rawNote &&
                length == note.length &&
                flags == note.flags;
        }

        public override int InsertionCompareTo(MoonObject obj)
        {
            int baseComp = base.InsertionCompareTo(obj);
            if (baseComp != 0 || obj is not MoonNote note)
                return baseComp;

            return rawNote.CompareTo(note.rawNote);
        }

        public bool isChord => (previous != null && previous.tick == tick) || (next != null && next.tick == tick);

        /// <summary>
        /// Ignores the note's forced flag when determining whether it would be a hopo or not
        /// </summary>
        public bool IsNaturalHopo(float hopoThreshold)
        {
            // Checking state in this order is important
            return !isChord &&
                previous != null &&
                (previous.isChord || rawNote != previous.rawNote) &&
                tick - previous.tick <= hopoThreshold;
        }

        /// <summary>
        /// Would this note be a hopo or not? (Ignores whether the note's tap flag is set or not.)
        /// </summary>
        public bool IsHopo(float hopoThreshold)
        {
            // F + F || T + T = strum
            return IsNaturalHopo(hopoThreshold) != forced;
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

                var note = this;
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

            foreach (var note in chord)
            {
                if (note.flags == flags)
                    mask |= 1 << note.rawNote;
            }

            return mask;
        }

        /// <summary>
        /// Live calculation of what Note_Type this note would currently be.
        /// </summary>
        public MoonNoteType GetGuitarNoteType(float hopoThreshold)
        {
            if ((flags & Flags.Tap) != 0)
            {
                return MoonNoteType.Tap;
            }
            return IsHopo(hopoThreshold) ? MoonNoteType.Hopo : MoonNoteType.Strum;
        }

        public MoonNoteType GetDrumsNoteType()
        {
            if (drumPad is DrumPad.Yellow or DrumPad.Blue or DrumPad.Orange &&
                (flags & Flags.ProDrums_Cymbal) != 0)
            {
                return MoonNoteType.Cymbal;
            }
            return MoonNoteType.Strum;
        }

        public class Chord : IEnumerable<MoonNote>
        {
            private readonly MoonNote _baseMoonNote;
            public Chord(MoonNote note) : base()
            {
                _baseMoonNote = note;
            }

            public IEnumerator<MoonNote> GetEnumerator()
            {
                var note = _baseMoonNote;

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

        public bool IsOpenNote(MoonChart.GameMode gameMode)
        {
            return gameMode switch
            {
                MoonChart.GameMode.Guitar => guitarFret == GuitarFret.Open,
                MoonChart.GameMode.GHLGuitar => ghliveGuitarFret == GHLiveGuitarFret.Open,
                MoonChart.GameMode.ProGuitar => proGuitarFret == 0,
                MoonChart.GameMode.Drums => drumPad == DrumPad.Kick,
                _ => false
            };
        }

        public static int MakeProGuitarRawNote(ProGuitarString proString, int fret)
        {
            fret = Math.Clamp(fret, 0, 22);
            int rawNote = (fret << PRO_GUITAR_FRET_OFFSET) & PRO_GUITAR_FRET_MASK;
            rawNote |= ((int)proString << PRO_GUITAR_STRING_OFFSET) & PRO_GUITAR_STRING_MASK;
            return rawNote;
        }

        protected override MoonObject CloneImpl() => Clone();

        public new MoonNote Clone()
        {
            return new MoonNote(tick, rawNote, length, flags);
        }

        public override string ToString()
        {
            return $"Note at tick {tick} with value {rawNote}, length {length}, flags {flags}";
        }
    }
}
