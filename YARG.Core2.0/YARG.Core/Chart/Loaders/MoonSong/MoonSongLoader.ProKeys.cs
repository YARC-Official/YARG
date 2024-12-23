using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;

namespace YARG.Core.Chart
{
    internal partial class MoonSongLoader : ISongLoader
    {
        public InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument instrument)
        {
            return LoadProKeysTrack(instrument, CreateProKeysNote);
        }

        private InstrumentTrack<ProKeysNote> LoadProKeysTrack(Instrument instrument, CreateNoteDelegate<ProKeysNote> createNote)
        {
            if (instrument.ToGameMode() != GameMode.ProKeys)
                throw new ArgumentException($"Instrument {instrument} is not a pro-keys instrument!", nameof(instrument));

            var difficulties = new Dictionary<Difficulty, InstrumentDifficulty<ProKeysNote>>
            {
                { Difficulty.Easy,   LoadDifficulty(instrument, Difficulty.Easy, createNote) },
                { Difficulty.Medium, LoadDifficulty(instrument, Difficulty.Medium, createNote) },
                { Difficulty.Hard,   LoadDifficulty(instrument, Difficulty.Hard, createNote) },
                { Difficulty.Expert, LoadDifficulty(instrument, Difficulty.Expert, createNote) },
            };
            return new(instrument, difficulties);
        }

        private ProKeysNote CreateProKeysNote(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var key = moonNote.proKeysKey;
            var generalFlags = GetGeneralFlags(moonNote, currentPhrases);
            var proKeysFlags = GetProKeysNoteFlags(moonNote, currentPhrases);

            double time = _moonSong.TickToTime(moonNote.tick);
            return new ProKeysNote(key, proKeysFlags, generalFlags, time, GetLengthInTime(moonNote),
                moonNote.tick, moonNote.length);
        }

        private ProKeysNoteFlags GetProKeysNoteFlags(MoonNote moonNote, Dictionary<MoonPhrase.Type, MoonPhrase> currentPhrases)
        {
            var flags = ProKeysNoteFlags.None;

            if (currentPhrases.TryGetValue(MoonPhrase.Type.ProKeys_Glissando, out var glissando) &&
                IsEventInPhrase(moonNote, glissando))
            {
                flags |= ProKeysNoteFlags.Glissando;
            }

            return flags;
        }
    }
}
