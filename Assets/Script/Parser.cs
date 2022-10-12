using System;
using System.Collections.Generic;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;

public static class Parser {
	public static void Parse(string midiFile, List<NoteInfo> outputTrack) {
		var midi = MidiFile.Read(midiFile);

		foreach (var trackChunk in midi.GetTrackChunks()) {
			foreach (var trackEvent in trackChunk.Events) {
				if (trackEvent is not SequenceTrackNameEvent trackName) {
					continue;
				}

				if (trackName.Text != "PART GUITAR") {
					break;
				}

				ParseGuitar(trackChunk.Events, midi.GetTempoMap(), outputTrack);
				break;
			}
		}

		// Sort by time
		Game.Instance.Chart.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));
	}

	private static void ParseGuitar(EventsCollection trackEvents, TempoMap tempo, List<NoteInfo> outputTrack) {
		long totalDelta = 0;
		foreach (var trackEvent in trackEvents) {
			totalDelta += trackEvent.DeltaTime;

			if (trackEvent is NoteOnEvent noteOnEvent) {
				// Expert octave
				if (noteOnEvent.GetNoteOctave() != 7) {
					continue;
				}

				// Convert note to fret number
				int fretNum = noteOnEvent.GetNoteName() switch {
					NoteName.C => 0,
					NoteName.CSharp => 1,
					NoteName.D => 2,
					NoteName.DSharp => 3,
					NoteName.E => 4,
					_ => -1
				};

				// Skip if not an actual note
				if (fretNum == -1) {
					continue;
				}

				// Convert delta to real time
				var metricTime = TimeConverter.ConvertTo<MetricTimeSpan>(totalDelta, tempo);
				float time = (float) metricTime.TotalSeconds;

				// Add to track
				outputTrack.Add(new NoteInfo(time, fretNum));
			}
		}
	}
}