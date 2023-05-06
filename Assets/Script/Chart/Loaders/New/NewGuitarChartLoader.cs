using System;
using System.Collections.Generic;
using MoonscraperChartEditor.Song;
using YARG.Data;

namespace YARG.Chart {
	public class NewGuitarChartLoader : ChartLoader<GuitarNote> {
		public NewGuitarChartLoader(MoonSong.MoonInstrument instrument) {
			InstrumentName = instrument switch {
				MoonSong.MoonInstrument.Guitar => "guitar",
				MoonSong.MoonInstrument.GuitarCoop => "guitarCoop",
				MoonSong.MoonInstrument.Rhythm => "rhythm",
				MoonSong.MoonInstrument.Bass => "bass",
				MoonSong.MoonInstrument.Keys => "keys",
				_ => throw new Exception("Instrument not supported!")
			};

			Instrument = instrument;
		}

		public override List<GuitarNote> GetNotesFromChart(MoonSong song, Difficulty difficulty) {
			var notes = new List<GuitarNote>();
			var chart = GetChart(song, difficulty);
			
			var starpowers = chart.starPower.ToArray();

			// Previous note (could be same tick)
			Note previousGameNote = null;
			
			// Previous note that is a different tick
			Note previousSeparateGameNote = null;

			Note parentNote = null;
			foreach (var moonNote in chart.notes) {
				var prevSeparateNote = moonNote.PreviousSeperateMoonNote;
				var nextSeparateNote = moonNote.NextSeperateMoonNote;

				var flags = NoteFlags.None;
				
				foreach (var starpower in starpowers) {
					uint spEndTick = starpower.tick + starpower.length;
					
					// Current note is within the bounds of current StarPower.
					// MoonNote tick is bigger or equal to StarPower tick
					// and smaller than StarPower end tick.
					if (moonNote.tick >= starpower.tick && moonNote.tick < spEndTick) {
						flags |= NoteFlags.StarPower;
						
						// If this is first note, or previous note is before starpower tick, mark as starpower start.
						if (prevSeparateNote is null || prevSeparateNote.tick < starpower.tick)
						{
							flags |= NoteFlags.StarPowerStart;
						}
						// If this is last note, or next note is not in starpower range, mark as end of starpower.
						if (nextSeparateNote is null || nextSeparateNote.tick >= spEndTick)
						{
							flags |= NoteFlags.StarPowerEnd;
						}
					}
				}
				
				// The tick this note has ended
				uint noteFinishTick = moonNote.tick + moonNote.length;
				if(nextSeparateNote is not null && nextSeparateNote.tick < noteFinishTick) {
					flags |= NoteFlags.ExtendedSustain;
				}
				if (moonNote.isChord) {
					flags |= NoteFlags.Chord;
				}

				int fret = MoonGuitarNoteToFret(moonNote);
				var currentNote = new GuitarNote(previousSeparateGameNote, moonNote.time, GetNoteLength(song, moonNote),
					moonNote.tick, moonNote.length, fret, moonNote.type, flags);
				
				// First note, must be a parent note
				if (previousGameNote is null) {
					parentNote = currentNote;

					previousGameNote = currentNote;
					notes.Add(currentNote);

					continue;
				}
				
				// Ticks don't match previous game note
				if (previousGameNote.Tick != currentNote.Tick) {
					parentNote = currentNote;

					previousSeparateGameNote = previousGameNote;

					previousSeparateGameNote.nextNote = currentNote;
					currentNote.previousNote = previousSeparateGameNote;
					
					notes.Add(currentNote);
				} else {
					// Add as a child note if ticks match
					parentNote.AddChildNote(currentNote);
				}

				previousGameNote = currentNote;
			}

			return notes;
		}

		private static int MoonGuitarNoteToFret(MoonNote note) {
			return note.guitarFret switch {
				MoonNote.GuitarFret.Open   => 0,
				MoonNote.GuitarFret.Green  => 1,
				MoonNote.GuitarFret.Red    => 2,
				MoonNote.GuitarFret.Yellow => 3,
				MoonNote.GuitarFret.Blue   => 4,
				MoonNote.GuitarFret.Orange => 5,
				_                          => 1
			};
		}
		
	}	
}
