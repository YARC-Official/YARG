using System;
using System.Collections.Generic;
using System.IO;
using MoonscraperChartEditor.Song.IO;
using NAudio.Midi;
using UnityEngine;
using YARG.Data;

namespace YARG.Song.Preparsers {
	public static class MidPreparser {
		
		private static readonly IReadOnlyDictionary<string, Instrument> PartLookup = new Dictionary<string, Instrument> {
			{ MidIOHelper.GUITAR_TRACK, Instrument.GUITAR },
			{ MidIOHelper.GH1_GUITAR_TRACK, Instrument.GUITAR },
			{ MidIOHelper.GUITAR_COOP_TRACK, Instrument.GUITAR_COOP },
			{ MidIOHelper.BASS_TRACK, Instrument.BASS },
			{ MidIOHelper.RHYTHM_TRACK, Instrument.RHYTHM },
			{ MidIOHelper.DRUMS_TRACK, Instrument.DRUMS },
			{ "PART DRUM", Instrument.DRUMS },
			{ MidIOHelper.DRUMS_REAL_TRACK, Instrument.REAL_DRUMS },
			{ MidIOHelper.KEYS_TRACK, Instrument.KEYS },
			{ MidIOHelper.VOCALS_TRACK, Instrument.VOCALS },
			{ "PART REAL_GUITAR", Instrument.REAL_GUITAR },
			{ "PART REAL_BASS", Instrument.REAL_BASS },
			
		};
		
		public static ulong GetAvailableTracks(byte[] chartData) {
			using var stream = new MemoryStream(chartData);
			try {
				var midi = new MidiFile(stream, false);
				return ReadStream(midi);
			} catch(Exception e) {
				Debug.LogError(e.Message);
				Debug.LogError(e.StackTrace);
				return ulong.MaxValue;
			}
		}
		
		public static ulong GetAvailableTracks(SongEntry song) {
			var midi = new MidiFile(Path.Combine(song.Location, song.NotesFile));

			return ReadStream(midi);
		}

		private static ulong ReadStream(MidiFile midi) {
			ulong tracks = 0;

			for (int i = 1; i < midi.Tracks; ++i) {
				var track = midi.Events[i];
				if (track == null || track.Count < 1)
				{
					continue;
				}

				if (track[0] is not TextEvent trackName)
					continue;

				string trackNameKey = trackName.Text.ToUpper();

				if (!PartLookup.TryGetValue(trackNameKey, out var instrument)) {
					continue;
				}
				
				int shiftAmount = (int)instrument * 4;
				tracks |= (uint)0xF << shiftAmount;
			}

			return tracks;
		}
	}
}