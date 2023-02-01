using System.Collections.Generic;
using YARG.Data;

namespace YARG.DiffDownsample {
	public static class FiveFretDownsample {
		private class ChordedNoteInfo {
			public float time;
			public float[] length;

			public FretFlag frets;
			public bool hopo;
			public bool autoHopo;
		}

		private static List<ChordedNoteInfo> ConsolidateToChords(List<NoteInfo> input) {
			// +1 due to the null at the beginning that is later removed
			var output = new List<ChordedNoteInfo>(input.Count + 1);

			// Consolidate
			float currentChordTime = -1f;
			ChordedNoteInfo currentChord = null;
			foreach (var note in input) {
				if (currentChordTime != note.time) {
					output.Add(currentChord);
					currentChordTime = note.time;

					// Start a new chord
					currentChord = new ChordedNoteInfo {
						time = note.time,
						length = new float[5],
						frets = (FretFlag) (1 << note.fret),
						hopo = note.hopo,
						autoHopo = note.autoHopo
					};

					// Set proper length
					currentChord.length[note.fret] = note.length;
				} else {
					currentChord.frets |= (FretFlag) (1 << note.fret);
					currentChord.length[note.fret] = note.length;
				}
			}

			// Remove null, add last chord
			output.Add(currentChord);
			output.RemoveAt(0);

			return output;
		}

		private static List<NoteInfo> SplitToNotes(List<ChordedNoteInfo> chords) {
			var output = new List<NoteInfo>();

			foreach (var chord in chords) {
				for (int i = 0; i < 5; i++) {
					if (!chord.frets.HasFlag((FretFlag) (1 << i))) {
						continue;
					}

					output.Add(new NoteInfo {
						time = chord.time,
						length = chord.length[i],
						fret = i,
						hopo = chord.hopo,
						autoHopo = chord.autoHopo
					});
				}
			}

			return output;
		}

		private static List<ChordedNoteInfo> CleanChords(List<ChordedNoteInfo> input) {
			FretFlag lastChord = FretFlag.NONE;
			foreach (var chord in input) {
				if (chord.hopo && chord.frets == lastChord) {
					chord.hopo = false;
					chord.autoHopo = false;
				}

				lastChord = chord.frets;
			}

			return input;
		}

		public static List<NoteInfo> DownsampleExpertToHard(List<NoteInfo> input) {
			var output = new List<NoteInfo>();

			// Remove some auto HOPOs

			int consecutiveRemovals = 0;
			foreach (var note in input) {
				bool maxConsecutiveReached = false;

				// Only skip up to 2 auto hopos
				if (note.autoHopo) {
					if (consecutiveRemovals < 2) {
						consecutiveRemovals++;
						continue;
					} else {
						consecutiveRemovals = 0;
						maxConsecutiveReached = true;
					}
				} else {
					consecutiveRemovals = 0;
				}

				// Clone
				var newNote = note.Duplicate();

				// Convert auto hopos to forced if max consecutive
				if (maxConsecutiveReached) {
					newNote.hopo = false;
				}

				output.Add(newNote);
			}

			// Remove 16th notes

			var chords = ConsolidateToChords(output);
			float lastTime = -1f;
			for (int i = 0; i < chords.Count; i++) {
				if (chords[i].time - lastTime <= 1f / 5f) {
					chords.RemoveAt(i);
					i--;
				} else {
					lastTime = chords[i].time;
				}
			}

			return SplitToNotes(CleanChords(chords));
		}
	}
}