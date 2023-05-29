using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using Melanchall.DryWetMidi.MusicTheory;
using UnityEngine;
using XboxSTFS;
using static XboxSTFS.XboxSTFSParser;
using YARG.Chart;
using YARG.Data;
using YARG.DiffDownsample;
using YARG.Song;

namespace YARG.Serialization.Parser {
	public partial class MidiParser : AbstractParser {
		private const bool FORCE_DOWNSAMPLE = false;

		private static readonly ReadingSettings ReadSettings = new() {
			InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
			NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
			NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
			InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
		};

		private struct EventIR {
			public long startTick;
			public long? endTick;
			public string name;
		}

		public MidiFile midi;

		public MidiParser(SongEntry songEntry, string[] files) : base(songEntry, files) {
			// get base midi - read it in latin1 if RB, UTF-8 if clon
			var readSettings = ReadSettings; // we need to modify these
			if (songEntry.SongType == SongType.RbCon) {
				var conSong = (ConSongEntry) songEntry;
				using var stream = new MemoryStream(XboxSTFSParser.GetFile(conSong.Location, conSong.FLMidi));
				readSettings.TextEncoding = Encoding.GetEncoding("iso-8859-1");
				midi = MidiFile.Read(stream, readSettings);
			} else if (songEntry.SongType == SongType.ExtractedRbCon) {
				readSettings.TextEncoding = Encoding.GetEncoding("iso-8859-1");
				midi = MidiFile.Read(files[0], readSettings);
			} else {
				readSettings.TextEncoding = Encoding.UTF8;
				midi = MidiFile.Read(files[0], readSettings);
			}

			// if this is a RB song...
			if (songEntry is ExtractedConSongEntry oof) {
				//...and it contains an update, merge the base and update midi
				if (oof.DiscUpdate) {
					List<string> BaseTracksToAdd = new List<string>();
					List<string> UpdateTracksToAdd = new List<string>();
					MidiFile midi_update = MidiFile.Read(oof.UpdateMidiPath, new ReadingSettings() { TextEncoding = Encoding.GetEncoding("iso-8859-1") });

					// get base track names
					foreach (var trackChunk in midi.GetTrackChunks()) {
						foreach (var trackEvent in trackChunk.Events) {
							if (trackEvent is not SequenceTrackNameEvent trackName) continue;
							BaseTracksToAdd.Add(trackName.Text);
							break;
						}
					}

					// get update track names
					foreach (var trackChunk in midi_update.GetTrackChunks()) {
						foreach (var trackEvent in trackChunk.Events) {
							if (trackEvent is not SequenceTrackNameEvent trackName) continue;
							UpdateTracksToAdd.Add(trackName.Text);
							// if a track is in both base and update, use the update track
							if (BaseTracksToAdd.Find(s => s == trackName.Text) != null) BaseTracksToAdd.Remove(trackName.Text);
							break;
						}
					}

					UpdateTracksToAdd.RemoveAt(0); // we want to stick with the base midi's tempomap

					// create new midi to use and set the tempo map to the base midi's
					MidiFile midi_merged = new MidiFile();
					midi_merged.ReplaceTempoMap(midi.GetTempoMap());

					// first, add approved base tracks to midi_merged
					foreach (var trackChunk in midi.GetTrackChunks()) {
						foreach (var trackEvent in trackChunk.Events) {
							if (trackEvent is not SequenceTrackNameEvent trackName) continue;
							if (BaseTracksToAdd.Find(s => s == trackName.Text) != null) midi_merged.Chunks.Add(trackChunk);
							break;
						}
					}
					// then, the update tracks
					foreach (var trackChunk in midi_update.GetTrackChunks()) {
						foreach (var trackEvent in trackChunk.Events) {
							if (trackEvent is not SequenceTrackNameEvent trackName) continue;
							if (UpdateTracksToAdd.Find(s => s == trackName.Text) != null) midi_merged.Chunks.Add(trackChunk);
							break;
						}
					}

					// finally, assign this new midi as the midi to use in-game
					midi = midi_merged;
				}

				// also, if this RB song has a pro upgrade, merge it as well
				if (oof.SongUpgrade.UpgradeMidiPath != string.Empty) {
					using var stream = new MemoryStream(oof.SongUpgrade.GetUpgradeMidi());
					MidiFile upgrade = MidiFile.Read(stream, new ReadingSettings() { TextEncoding = Encoding.GetEncoding("iso-8859-1") });

					foreach (var trackChunk in upgrade.GetTrackChunks()) {
						foreach (var trackEvent in trackChunk.Events) {
							if (trackEvent is not SequenceTrackNameEvent trackName) continue;
							if (trackName.Text.Contains("PART REAL_GUITAR") || trackName.Text.Contains("PART REAL_BASS")) {
								midi.Chunks.Add(trackChunk);
							}
						}
					}

				}
			}
		}

		public override void Parse(YargChart chart) {
			var eventIR = new List<EventIR>();
			var tempo = midi.GetTempoMap();

			TrackChunk harm1Chunk = null;
			foreach (var trackChunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in trackChunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					// Parse each chunk
					try {
						// Parse harmony parts
						if (trackName.Text.StartsWith("HARM") || trackName.Text.StartsWith("PART HARM")) {
							// Get the harmony index from name
							int harmIndex;
							if (trackName.Text.StartsWith("HARM")) {
								harmIndex = int.Parse(trackName.Text[4..^0]) - 1;
							} else {
								harmIndex = int.Parse(trackName.Text[9..^0]) - 1;
							}

							if (harmIndex > 2) {
								continue;
							}

							// Expand/create the array if necessary
							if (chart.harmLyrics.Length <= harmIndex) {
								Array.Resize(ref chart.harmLyrics, harmIndex + 1);
							}

							// Parse the harmony part
							if (harmIndex == 0) {
								chart.harmLyrics[harmIndex] = ParseRealLyrics(eventIR, trackChunk, tempo, harmIndex);
							} else if (harmIndex == 1) {
								chart.harmLyrics[harmIndex] = ParseRealLyrics(eventIR, trackChunk, tempo, harmIndex);
								harm1Chunk = trackChunk;
							} else if (harmIndex == 2) {
								chart.harmLyrics[harmIndex] = ParseRealLyrics(eventIR, trackChunk, harm1Chunk, tempo, harmIndex);
							}
							continue;
						}

						// Parse everything else
						switch (trackName.Text) {
							case "PART GUITAR":
								for (int i = 0; i < 4; i++) {
									chart.Guitar[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "guitar");
								ParseSolo(eventIR, trackChunk, "guitar");
								break;
							case "PART GUITAR COOP":
								for (int i = 0; i < 4; i++) {
									chart.GuitarCoop[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "guitarCoop");
								ParseSolo(eventIR, trackChunk, "guitarCoop");
								break;
							case "PART RHYTHM":
								for (int i = 0; i < 4; i++) {
									chart.Rhythm[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "rhythm");
								ParseSolo(eventIR, trackChunk, "rhythm");
								break;
							case "PART BASS":
								for (int i = 0; i < 4; i++) {
									chart.Bass[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "bass");
								ParseSolo(eventIR, trackChunk, "bass");
								break;
							case "PART KEYS":
								for (int i = 0; i < 4; i++) {
									chart.Keys[i] = ParseFiveFret(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "keys");
								ParseSolo(eventIR, trackChunk, "keys");
								break;
							case "PART VOCALS":
								chart.genericLyrics = ParseGenericLyrics(trackChunk, tempo);
								chart.realLyrics = ParseRealLyrics(eventIR, trackChunk, tempo, -1);
								ParseStarpower(eventIR, trackChunk, "vocals");
								break;
							case "PART REAL_GUITAR":
								for (int i = 0; i < 4; i++) {
									chart.RealGuitar[i] = ParseRealGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "realGuitar");
								ParseSolo(eventIR, trackChunk, "realGuitar", 8);
								break;
							case "PART REAL_BASS":
								for (int i = 0; i < 4; i++) {
									chart.RealBass[i] = ParseRealGuitar(trackChunk, i);
								}
								ParseStarpower(eventIR, trackChunk, "realBass");
								ParseSolo(eventIR, trackChunk, "realBass", 8);
								break;
							case "PART DRUM":
							case "PART DRUMS":
								var drumType = GetDrumType(trackChunk);

								if (drumType == DrumType.FourLane) {
									for (int i = 0; i < 5; i++) {
										chart.Drums[i] = ParseDrums(trackChunk, false, i, drumType, null);
										chart.RealDrums[i] = ParseDrums(trackChunk, true, i, drumType, null);

										chart.GhDrums[i] = ParseGHDrums(trackChunk, i, drumType, chart.RealDrums[i]);
										ParseStarpower(eventIR, trackChunk, "drums");
										ParseStarpower(eventIR, trackChunk, "realDrums");
										ParseDrumFills(eventIR, trackChunk, "drums");
										ParseDrumFills(eventIR, trackChunk, "realDrums");
									}
								} else {
									for (int i = 0; i < 5; i++) {
										chart.GhDrums[i] = ParseGHDrums(trackChunk, i, drumType, null);

										chart.Drums[i] = ParseDrums(trackChunk, false, i, drumType, chart.GhDrums[i]);
										chart.RealDrums[i] = ParseDrums(trackChunk, true, i, drumType, chart.GhDrums[i]);

										// TODO: SP is still a bit broken on 5-lane and is therefore disabled for now
										//ParseStarpower(eventIR, trackChunk, "ghDrums");
										//ParseDrumFills(eventIR, trackChunk, "ghDrums");
									}
								}
								break;
							case "BEAT":
								ParseBeats(eventIR, trackChunk);
								break;
							case "VENUE":
								ParseVenue(eventIR, trackChunk);
								break;
						}
					} catch (Exception e) {
						Debug.LogError($"Error while parsing track chunk named `{trackName.Text}`. Skipped.");
						Debug.LogException(e);
					}
				}
			}

			// Downsample instruments

			foreach (var subChart in chart.allParts) {
				try {
					// Downsample Five Fret instruments
					if (subChart == chart.Guitar || subChart == chart.Bass || subChart == chart.Keys) {
						if (subChart[3].Count >= 1 && (subChart[2].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[2] = FiveFretDownsample.DownsampleExpertToHard(subChart[3]);
							Debug.Log("Downsampled expert to hard.");
						}

						if (subChart[2].Count >= 1 && (subChart[1].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[1] = FiveFretDownsample.DownsampleHardToMedium(subChart[2]);
							Debug.Log("Downsampled hard to normal.");
						}

						if (subChart[1].Count >= 1 && (subChart[0].Count <= 0 || FORCE_DOWNSAMPLE)) {
							subChart[0] = FiveFretDownsample.DownsampleMediumToEasy(subChart[1]);
							Debug.Log("Downsampled normal to easy.");
						}
					}
				} catch (Exception e) {
					Debug.LogError("Error while downsampling. Skipped.");
					Debug.LogException(e);
				}
			}

			// Sort notes by time (just in case) and add delay

			float lastNoteTime = 0f;
			foreach (var part in chart.allParts) {
				foreach (var difficulty in part) {
					if (difficulty == null || difficulty.Count <= 0) {
						continue;
					}

					// Sort
					difficulty.Sort(new Comparison<NoteInfo>((a, b) => a.time.CompareTo(b.time)));

					// Add delay
					foreach (var note in difficulty) {
						note.time += (float) songEntry.Delay;
					}

					// Last note time
					if (difficulty[^1].EndTime > lastNoteTime) {
						lastNoteTime = difficulty[^1].EndTime;
					}
				}
			}

			// Add delay to vocals

			foreach (var lyric in chart.genericLyrics) {
				lyric.time += (float) songEntry.Delay;
			}

			foreach (var lyric in chart.realLyrics) {
				lyric.time += (float) songEntry.Delay;
			}

			foreach (var lyricList in chart.harmLyrics) {
				foreach (var lyric in lyricList) {
					lyric.time += (float) songEntry.Delay;
				}
			}

			// Generate beat line events if there aren't any

			if (!eventIR.Any(i => i.name == "beatLine_minor" || i.name == "beatLine_major")) {
				GenerateBeats(eventIR, tempo, lastNoteTime);
			}

			// Convert event IR into real

			chart.events = new();

			foreach (var eventInfo in eventIR) {
				float startTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.startTick, tempo).TotalSeconds;

				// If the event has length, do that too
				if (eventInfo.endTick.HasValue) {
					float endTime = (float) TimeConverter.ConvertTo<MetricTimeSpan>(eventInfo.endTick.Value, tempo).TotalSeconds;
					chart.events.Add(new EventInfo(eventInfo.name, startTime, endTime - startTime));
				} else {
					chart.events.Add(new EventInfo(eventInfo.name, startTime));
				}
			}

			// Sort events by time (required) and add delay

			chart.events.Sort(new Comparison<EventInfo>((a, b) => a.time.CompareTo(b.time)));
			foreach (var ev in chart.events) {
				ev.time += (float) songEntry.Delay;
			}

			// Add beats to chart

			chart.beats = new();
			foreach (var ev in chart.events) {
				if (ev.name is "beatLine_minor") {
					chart.beats.Add(new Beat {
						Time = ev.time,
						Style = BeatStyle.STRONG,
					});
				} else if (ev.name is "beatLine_major") {
					chart.beats.Add(new Beat {
						Time = ev.time,
						Style = BeatStyle.MEASURE,
					});
				}
			}

			// Look for bonus star power

			// TODO
		}

		private void ParseStarpower(List<EventIR> eventIR, TrackChunk trackChunk, string instrument) {
			long totalDelta = 0;

			long? starPowerStart = null;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != 8) {
					continue;
				}

				// Skip if not a star power event
				if (noteEvent.GetNoteName() != NoteName.GSharp) {
					continue;
				}

				if (trackEvent is NoteOnEvent) {
					// We need to know when it ends before adding it
					starPowerStart = totalDelta;
				} else if (trackEvent is NoteOffEvent) {
					if (starPowerStart == null) {
						continue;
					}

					// Now that we know the start and end, add it to the list of events.
					eventIR.Add(new EventIR {
						startTick = starPowerStart.Value,
						endTick = totalDelta,
						name = $"starpower_{instrument}"
					});
					starPowerStart = null;
				}
			}
		}

		private void ParseSolo(List<EventIR> eventIR, TrackChunk trackChunk, string instrument, int soloOctave = 7) {
			long totalDelta = 0;
			long? soloStart = null;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != soloOctave) {
					continue;
				}

				// Skip if not a star power event
				if (noteEvent.GetNoteName() != NoteName.G) {
					continue;
				}

				if (trackEvent is NoteOnEvent) {
					// We need to know when it ends before adding it
					soloStart = totalDelta;
				} else if (trackEvent is NoteOffEvent) {
					if (soloStart == null) {
						continue;
					}

					// Now that we know the start and end, add it to the list of events.
					eventIR.Add(new EventIR {
						startTick = soloStart.Value,
						endTick = totalDelta,
						name = $"solo_{instrument}"
					});
					soloStart = null;
				}
			}
		}

		private void ParseDrumFills(List<EventIR> eventIR, TrackChunk trackChunk, string instrument) {
			long totalDelta = 0;

			long? fillStart = null;

			// Convert track events into intermediate representation
			foreach (var trackEvent in trackChunk.Events) {
				totalDelta += trackEvent.DeltaTime;

				if (trackEvent is not NoteEvent noteEvent) {
					continue;
				}

				// Look for correct octave
				if (noteEvent.GetNoteOctave() != 9) {
					continue;
				}

				// Skip if not a star power event
				if (noteEvent.GetNoteName() != NoteName.B
					&& noteEvent.GetNoteName() != NoteName.C
					&& noteEvent.GetNoteName() != NoteName.D
					&& noteEvent.GetNoteName() != NoteName.E
					) {
					continue;
				}

				if (trackEvent is NoteOnEvent) {
					// We need to know when it ends before adding it
					fillStart = totalDelta;
				} else if (trackEvent is NoteOffEvent) {
					if (fillStart == null) {
						continue;
					}

					// Now that we know the start and end, add it to the list of events.
					eventIR.Add(new EventIR {
						startTick = fillStart.Value,
						endTick = totalDelta,
						name = $"fill_{instrument}"
					});
					fillStart = null;
				}
			}
		}

		private DrumType GetDrumType(TrackChunk trackChunk) {
			if (songEntry.DrumType != DrumType.Unknown) {
				return songEntry.DrumType;
			}

			// If we don't know the drum type...
			foreach (var midiEvent in trackChunk.Events) {
				if (midiEvent is not NoteEvent note) {
					continue;
				}

				// Look for the expert 5th-lane note
				if (note.NoteNumber == 101) {
					return DrumType.FiveLane;
				}
			}

			// If we didn't find the note, assume 4-lane
			return DrumType.FourLane;
		}
	}
}
