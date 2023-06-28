using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart
{
    public class NewDrumChartLoader : ChartLoader<DrumNote>
    {
        private readonly bool _isPro;

        public NewDrumChartLoader(bool isPro)
        {
            _isPro = isPro;

            Instrument = MoonSong.MoonInstrument.Drums;
            InstrumentName = _isPro ? "realDrums" : "drums";
            MaxDifficulty = Difficulty.EXPERT_PLUS;
        }

        public override List<DrumNote> GetNotesFromChart(MoonSong song, Difficulty difficulty)
        {
            var notes = new List<DrumNote>();
            var chart = GetChart(song, difficulty);
            bool doubleBass = difficulty == Difficulty.EXPERT_PLUS;

            // do star power later lol idk how it works

            // Previous note (could be same tick)
            Note previousGameNote = null;

            // Previous note that is a different tick
            Note previousSeparateGameNote = null;

            Note parentNote = null;
            foreach (var moonNote in chart.notes)
            {
                var flags = NoteFlags.None;

                if ((moonNote.flags & MoonNote.Flags.ProDrums_Cymbal) != 0 && _isPro)
                {
                    flags |= NoteFlags.Cymbal;
                }

                // Is kick, is double kick note but double bass not active
                // Skip the note
                if (moonNote.drumPad == MoonNote.DrumPad.Kick && (moonNote.flags & MoonNote.Flags.DoubleKick) != 0 &&
                    !doubleBass)
                {
                    continue;
                }

                int fret = MoonDrumNoteToPad(moonNote);
                var currentNote = new DrumNote(previousSeparateGameNote, moonNote.time, moonNote.tick, fret, flags);

                // First note, must be a parent note
                if (previousGameNote is null)
                {
                    parentNote = currentNote;

                    previousGameNote = currentNote;
                    notes.Add(currentNote);

                    continue;
                }

                // Ticks don't match previous game note
                if (previousGameNote.Tick != currentNote.Tick)
                {
                    parentNote = currentNote;

                    previousSeparateGameNote = previousGameNote;

                    previousSeparateGameNote.nextNote = currentNote;
                    currentNote.previousNote = previousSeparateGameNote;

                    notes.Add(currentNote);
                }
                else
                {
                    // Add as a child note if ticks match
                    parentNote.AddChildNote(currentNote);
                }

                previousGameNote = currentNote;
            }

            return notes;
        }

        private static int MoonDrumNoteToPad(MoonNote note)
        {
            return note.drumPad switch
            {
                MoonNote.DrumPad.Kick   => 0,
                MoonNote.DrumPad.Green  => 1,
                MoonNote.DrumPad.Red    => 2,
                MoonNote.DrumPad.Yellow => 3,
                MoonNote.DrumPad.Blue   => 4,
                MoonNote.DrumPad.Orange => 5,
                _                       => 1
            };
        }
    }
}