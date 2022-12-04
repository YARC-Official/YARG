using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

namespace YARG.Serialization {
	public class MidiParser : AbstractParser {
		private enum ForceState {
			NONE,
			HOPO,
			AUTO_HOPO,
			STRUM
		}

		private struct EventIR {
			public long startTick;
			public string name;
		}

		private struct NoteIR {
			public long startTick;
			public long endTick;
			public int fret;

			public ForceState forceState;
			public bool isChord;
		}

		public MidiFile midi;

		public MidiParser(string file) : base(file) {
			midi = MidiFile.Read(file);
		}

		public override void Parse(out List<NoteInfo> chartNotes, out List<EventInfo> chartEvents) {
			chartNotes = null;
			chartEvents = null;
			foreach (var trackChunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in trackChunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					if (trackName.Text == "PART GUITAR") {
						chartNotes = ParseGuitar(trackChunk);
					}

					if (trackName.Text == "BEAT") {
						chartEvents = ParseBeats(trackChunk);
					}
				}
			}

			// Sort by time (just in case)
			chartNotes?.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
			chartEvents?.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
		}

		private List<NoteInfo> ParseGuitar(TrackChunk trackChunk) {
			var tempo = midi.GetTempoMap();

			long totalDelta = 0;

			var noteIR = new List<NoteIR>();
			long?[] fretState = new long?[5];
			var forceState = ForceState.NONE;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Expert octave
				if (noteEvent.GetNoteOctave() != 7) {
					continue;
				}

				// Convert note to fret number (or special)
				int fretNum = noteEvent.GetNoteName() switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					NoteName.D => 2,
					NoteName.DSharp => 3,
					NoteName.E => 4,
					NoteName.F => 5,
					NoteName.FSharp => 6,
					_ => -1
				};

				// Skip if not an actual note
				if (fretNum == -1) {
					continue;
				}

				// Handle special
				if (fretNum == 5) {
					forceState = noteEvent is NoteOnEvent ? ForceState.HOPO : ForceState.NONE;
					continue;
				} else if (fretNum == 6) {
					forceState = noteEvent is NoteOnEvent ? ForceState.STRUM : ForceState.NONE;
					continue;
				}

				// Deal with note ons or note offs

				if (fretState[fretNum] != null) {
					var noteForceState = forceState;
					bool isChord = false;

					if (noteForceState == ForceState.NONE && noteIR.Count > 0) {
						var lastNote = noteIR[^1];

						// Check for chord
						if (lastNote.startTick == fretState[fretNum].Value) {
							isChord = true;

							// Check for missing first note of chord
							if (!lastNote.isChord) {
								lastNote.isChord = true;
								lastNote.forceState = lastNote.forceState == ForceState.AUTO_HOPO ?
									ForceState.NONE :
									lastNote.forceState;

								noteIR[^1] = lastNote;
							}
						}

						// Check for auto HOPO and chord
						if (lastNote.fret != fretNum && !isChord) {
							var lastNoteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(lastNote.startTick, tempo);
							var noteBeat = TimeConverter.ConvertTo<MusicalTimeSpan>(fretState[fretNum].Value, tempo);
							var distance = noteBeat - lastNoteBeat;

							if (distance <= MusicalTimeSpan.Sixteenth) {
								noteForceState = ForceState.AUTO_HOPO;
							}
						}
					}

					noteIR.Add(new NoteIR {
						startTick = fretState[fretNum].Value,
						endTick = totalDelta,
						fret = fretNum,
						forceState = noteForceState,
						isChord = isChord
					});
				}

				if (noteEvent is NoteOnEvent) {
					fretState[fretNum] = totalDelta;
				} else {
					fretState[fretNum] = null;
				}
			}

			var noteOutput = new List<NoteInfo>(noteIR.Count);

			// IR into real
			foreach (var noteInfo in noteIR) {
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.startTick, tempo).TotalSeconds;
				float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(noteInfo.endTick, tempo).TotalSeconds;

				bool hopo = noteInfo.forceState == ForceState.HOPO
					|| noteInfo.forceState == ForceState.AUTO_HOPO;
				noteOutput.Add(new NoteInfo(startTime, noteInfo.fret, endTime - startTime, hopo));
			}

			return noteOutput;
		}

		private List<EventInfo> ParseBeats(TrackChunk trackChunk) {
			var eventIR = new List<EventIR>();
			long totalDelta = 0;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteOnEvent noteOnEvent) {
					continue;
				}

				// Convert note to beat line type
				int majorOrMinor = noteOnEvent.GetNoteName() switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					_ => -1
				};

				// Skip if not a beat line
				if (majorOrMinor == -1) {
					continue;
				}

				if (majorOrMinor == 1) {
					eventIR.Add(new EventIR {
						startTick = totalDelta,
						name = "beatLine_minor"
					});
				} else {
					eventIR.Add(new EventIR {
						startTick = totalDelta,
						name = "beatLine_major"
					});
				}
			}

			var eventOutput = new List<EventInfo>(eventIR.Count);
			var tempo = midi.GetTempoMap();

			// IR into real
			foreach (var eventInfo in eventIR) {
				float time = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.startTick, tempo).TotalSeconds;
				eventOutput.Add(new EventInfo(time, eventInfo.name));
			}

			return eventOutput;
		}
	}
}