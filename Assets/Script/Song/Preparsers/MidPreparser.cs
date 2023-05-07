using System;
using System.Collections.Generic;
using System.IO;
using Melanchall.DryWetMidi.Core;
using MoonscraperChartEditor.Song.IO;
using UnityEngine;
using YARG.Data;

namespace YARG.Song.Preparsers {
	public static class MidPreparser {

		private static readonly ReadingSettings ReadSettings = new() {
			InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
			NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
			NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
			InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
		};

		private static readonly IReadOnlyDictionary<string, Instrument> PartLookup = new Dictionary<string, Instrument> {
			{ MidIOHelper.GUITAR_TRACK, Instrument.GUITAR },
			{ MidIOHelper.GH1_GUITAR_TRACK, Instrument.GUITAR },
			{ MidIOHelper.GUITAR_COOP_TRACK, Instrument.GUITAR_COOP },
			{ MidIOHelper.BASS_TRACK, Instrument.BASS },
			{ MidIOHelper.RHYTHM_TRACK, Instrument.RHYTHM },
			{ MidIOHelper.DRUMS_TRACK, Instrument.DRUMS },
			{ "PART DRUM", Instrument.DRUMS },
			{ MidIOHelper.KEYS_TRACK, Instrument.KEYS },
			{ MidIOHelper.VOCALS_TRACK, Instrument.VOCALS },
			{ "PART REAL_GUITAR", Instrument.REAL_GUITAR },
			{ "PART REAL_BASS", Instrument.REAL_BASS },

		};

		public static ulong GetAvailableTracks(byte[] chartData) {
			using var stream = new MemoryStream(chartData);
			try {
				var midi = MidiFile.Read(stream, ReadSettings);
				return ReadStream(midi);
			} catch (Exception e) {
				Debug.LogError(e.Message);
				Debug.LogError(e.StackTrace);
				return ulong.MaxValue;
			}
		}

		public static ulong GetAvailableTracks(SongEntry song) {
			try {
				var midi = MidiFile.Read(Path.Combine(song.Location, song.NotesFile), ReadSettings);
				return ReadStream(midi);
			} catch (Exception e) {
				Debug.LogError(e.Message);
				Debug.LogError(e.StackTrace);
				return ulong.MaxValue;
			}
		}

		private static ulong ReadStream(MidiFile midi) {
			ulong tracks = 0;

			foreach (var chunk in midi.GetTrackChunks()) {
				foreach (var trackEvent in chunk.Events) {
					if (trackEvent is not SequenceTrackNameEvent trackName) {
						continue;
					}

					string trackNameKey = trackName.Text.ToUpper();

					if (!PartLookup.TryGetValue(trackNameKey, out var instrument)) {
						continue;
					}

					int shiftAmount = (int)instrument * 4;
					tracks |= 0xFUL << shiftAmount;
				}
			}

			return tracks;
		}
	}
}