using EasySharpIni;
using EasySharpIni.Converters;
using EasySharpIni.Models;
using YARG.Data;

namespace YARG.Song {
	public static class ScanHelpers {

		private static readonly IntConverter IntConverter = new();

		public static ScanResult ParseSongIni(string iniFile, IniSongEntry entry) {
			var file = new IniFile(iniFile);
			file.Parse();

			string sectionName = file.ContainsSection("song") ? "song" : "Song";

			if (!file.ContainsSection(sectionName)) {
				return ScanResult.NotASong;
			}

			var section = file.GetSection(sectionName);

			entry.Name = section.GetField("name");
			entry.Artist = section.GetField("artist");
			entry.Charter = section.GetField("charter");

			entry.Album = section.GetField("album");
			entry.AlbumTrack = section.GetField("album_track", "0").Get(IntConverter);
			if (section.ContainsField("track")) {
				entry.AlbumTrack = section.GetField("track", "0").Get(IntConverter);
			}

			entry.PlaylistTrack = section.GetField("playlist_track", "0").Get(IntConverter);

			entry.Genre = section.GetField("genre");
			entry.Year = section.GetField("year");

			entry.SongLength = section.GetField("song_length", "-1").Get(IntConverter);
			entry.PreviewStart = section.GetField("preview_start_time", "0").Get(IntConverter);
			entry.PreviewEnd = section.GetField("preview_end", "-1").Get(IntConverter);

			int rawDelay = section.GetField("delay").Get(IntConverter);
			entry.Delay = rawDelay / 1000.0;

			entry.HopoThreshold = section.GetField("hopo_frequency", "170").Get(IntConverter);
			entry.EighthNoteHopo = section.GetField("eighthnote_hopo", "false").Get().ToLower() == "true";
			entry.MultiplierNote = section.GetField("multiplier_note", "116").Get(IntConverter);

			ReadDifficulties(section, entry);
			
			if (section.ContainsField("pro_drums")) {
				switch (section.GetField("pro_drums")) {
					case "true":
					case "1":
						entry.DrumType = DrumType.FourLane;
						break;
				}
			} else if (section.ContainsField("five_lane_drums")) {
				switch (section.GetField("five_lane_drums")) {
					case "true":
					case "1":
						entry.DrumType = DrumType.FiveLane;
						break;
				}
			} else {
				entry.DrumType = DrumType.Unknown;
			}

			entry.LoadingPhrase = section.GetField("loading_phrase");
			entry.Source = section.GetField("icon");
			entry.HasLyrics = section.GetField("lyrics").Get().ToLower() == "true";
			entry.IsModChart = section.GetField("modchart").Get().ToLower() == "true";

			return ScanResult.Ok;
		}

		private static void ReadDifficulties(IniSection section, SongEntry entry) {
			entry.PartDifficulties.Clear();

			entry.PartDifficulties.Add(Instrument.GUITAR, section.GetField("diff_guitar", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.GUITAR_COOP, section.GetField("diff_guitar_coop", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.REAL_GUITAR, section.GetField("diff_guitar_real", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.RHYTHM, section.GetField("diff_rhythm", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.BASS, section.GetField("diff_bass", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.REAL_BASS, section.GetField("diff_bass_real", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.DRUMS, section.GetField("diff_drums", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.GH_DRUMS, section.GetField("diff_drums", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.REAL_DRUMS, section.GetField("diff_drums_real", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.KEYS, section.GetField("diff_keys", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.REAL_KEYS, section.GetField("diff_keys_real", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.VOCALS, section.GetField("diff_vocals", "-1").Get(IntConverter));
			entry.PartDifficulties.Add(Instrument.HARMONY, section.GetField("diff_vocals_harm", "-1").Get(IntConverter));
		}
	}
}