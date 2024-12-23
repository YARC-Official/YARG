using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<ProGuitarNote> LoadProGuitarTrack(Instrument instrument)
        {
            if (instrument.ToGameMode() != GameMode.ProGuitar)
                throw new ArgumentException($"Instrument {instrument} is not a Pro guitar instrument!", nameof(instrument));

            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<ProGuitarNote>>()
            {
                { Difficulty.Easy, LoadDifficulty(instrument, Difficulty.Easy, CreateProGuitarNote) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, CreateProGuitarNote) },
                { Difficulty.Hard, LoadDifficulty(instrument, Difficulty.Hard, CreateProGuitarNote) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, CreateProGuitarNote) },
            };
            return new(instrument, difficulties);
        }

        private ProGuitarNote CreateProGuitarNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var proString = GetProGuitarString(moonNote);
            int proFret = GetProGuitarFret(moonNote);
            var noteType = GetProGuitarNoteType(moonNote);
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var proFlags = GetProGuitarNoteFlags(moonNote);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new ProGuitarNote(proString, proFret, noteType, proFlags, generalFlags, time, GetLengthInTime(moonNote), moonNote.tick, moonNote.length);
        }

        private ProGuitarString GetProGuitarString(MoonNote moonNote)
        {
            return moonNote.proGuitarString switch
            {
                MoonNote.ProGuitarString.Red    => ProGuitarString.Red,
                MoonNote.ProGuitarString.Green  => ProGuitarString.Green,
                MoonNote.ProGuitarString.Orange => ProGuitarString.Orange,
                MoonNote.ProGuitarString.Blue   => ProGuitarString.Blue,
                MoonNote.ProGuitarString.Yellow => ProGuitarString.Yellow,
                MoonNote.ProGuitarString.Purple => ProGuitarString.Purple,
                _ => throw new InvalidOperationException($"Unhandled Moonscraper Pro guitar string {moonNote.proGuitarString}!")
            };
        }

        private int GetProGuitarFret(MoonNote moonNote)
        {
            return moonNote.proGuitarFret;
        }

        private ProGuitarNoteType GetProGuitarNoteType(MoonNote moonNote)
        {
            var type = moonNote.GetGuitarNoteType(_moonSong.hopoThreshold);
            return type switch
            {
                MoonNote.MoonNoteType.Strum => ProGuitarNoteType.Strum,
                MoonNote.MoonNoteType.Hopo  => ProGuitarNoteType.Hopo,
                MoonNote.MoonNoteType.Tap   => ProGuitarNoteType.Tap,
                _ => throw new InvalidOperationException($"Unhandled Moonscraper note type {type}!")
            };
        }

        private ProGuitarNoteFlags GetProGuitarNoteFlags(MoonNote moonNote)
        {
            var flags = ProGuitarNoteFlags.None;

            // Standard guitar flags
            var guitarFlags = GetGuitarNoteFlags(moonNote);
            if ((guitarFlags & GuitarNoteFlags.ExtendedSustain) != 0)
                flags |= ProGuitarNoteFlags.ExtendedSustain;
            if ((guitarFlags & GuitarNoteFlags.Disjoint) != 0)
                flags |= ProGuitarNoteFlags.Disjoint;

            // Muted notes
            if ((moonNote.flags & MoonNote.Flags.ProGuitar_Muted) != 0)
            {
                flags |= ProGuitarNoteFlags.Muted;
            }

            return flags;
        }
    }
}