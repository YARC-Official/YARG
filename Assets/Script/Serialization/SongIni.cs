using System;
using System.IO;
using System.Text;
using IniParser;
using IniParser.Model;
using UnityEngine;
using YARG.Data;

namespace YARG.Serialization {
	public static class SongIni {
		private static readonly FileIniDataParser PARSER = new();

		static SongIni() {
			PARSER.Parser.Configuration.AllowDuplicateKeys = true;

			// Only match "//" and ";" as comments
			PARSER.Parser.Configuration.CommentRegex = new(@"^//(.*)|^;(.*)");
		}

		public static void CompleteSongInfo(SongInfo song) {
			if (song.fetched) {
				return;
			}

			var filePath = Path.Combine(song.RootFolder, "song.ini");
			if (!File.Exists(filePath)) {
				return;
			}

			// Read the file
			song.fetched = true;
			var data = PARSER.ReadFile(filePath, Encoding.UTF8);

			// Get song section name
			KeyDataCollection section;
			if (data.Sections.ContainsSection("song")) {
				section = data["song"];
			} else if (data.Sections.ContainsSection("Song")) {
				section = data["Song"];
			} else {
				Debug.LogError($"No `song` section found in `{song.RootFolder}`.");
				throw new ArgumentException($"No `song` section found in `{song.RootFolder}`.");
			}

			// Set basic info
			song.SongName = section["name"];
			song.artistName = section["artist"];

			// Get other metadata
			song.album = section.GetKeyData("album")?.Value;
			song.genre = section.GetKeyData("genre")?.Value;
			song.year = section.GetKeyData("year")?.Value;
			song.loadingPhrase = section.GetKeyData("loading_phrase")?.Value;

			// Get charter
			if (section.ContainsKey("charter")) {
				song.charter = section["charter"];
			} else if (section.ContainsKey("frets")) {
				song.charter = section["frets"];
			}

			// Get song source
			if (section.ContainsKey("icon") && section["icon"] != "0") {
				song.source ??= section["icon"];
			} else {
				song.source = "custom";
			}

			// Get song length
			if (section.ContainsKey("song_length")) {
				int rawLength = int.Parse(section["song_length"]);
				song.songLength = rawLength / 1000f;
			} else {
				Debug.LogWarning($"No song length found for `{song.RootFolder}`.");
			}

			// Get drum type
			if (section.ContainsKey("pro_drums") && (
				section["pro_drums"].ToLowerInvariant() == "true" ||
				section["pro_drums"] == "1")) {

				song.drumType = SongInfo.DrumType.FOUR_LANE;
			} else if (section.ContainsKey("five_lane_drums") && (
				section["five_lane_drums"].ToLowerInvariant() == "true" ||
				section["five_lane_drums"] == "1")) {

				song.drumType = SongInfo.DrumType.FIVE_LANE;
			} else {
				song.drumType = SongInfo.DrumType.UNKNOWN;
			}

			// Get song delay (0 if none)
			if (section.ContainsKey("delay")) {
				int rawDelay = int.Parse(section["delay"]);
				song.delay = rawDelay / 1000f;
			} else {
				song.delay = 0f;
			}

			// Get hopo frequency
			// Standardized here: https://github.com/TheNathannator/GuitarGame_ChartFormats/blob/main/doc/FileFormats/.mid/Standard/5-Fret%20Guitar.md#note-mechanics
			if (section.ContainsKey("hopo_frequency")) {
				song.hopoFreq = int.Parse(section["hopo_frequency"]);
			} else if (section.ContainsKey("hopofreq")) {
				song.hopoFreq = int.Parse(section["hopofreq"]);
			} else if (section.ContainsKey("eighthnote_hopo")) {
				if (section["eighthnote_hopo"].ToLowerInvariant() == "true" ||
					section["eighthnote_hopo"] == "1") {

					song.hopoFreq = 240;
				}
			}

			// Get difficulties
			bool noneFound = true;
			foreach (Instrument instrument in Enum.GetValues(typeof(Instrument))) {
				var key = instrument.ToSongIniName();
				if (key == null) {
					continue;
				}

				if (section.ContainsKey(key)) {
					song.partDifficulties[instrument] = int.Parse(section[key]);
					noneFound = true;
				}
			}

			// If no difficulties found, check the source
			// TODO: Check midi file instead
			if (noneFound) {
				if (song.source == "gh1") {
					song.partDifficulties[Instrument.GUITAR] = -2;
				} else if (song.source == "gh2"
					|| song.source == "gh80s"
					|| song.source == "gh3"
					|| song.source == "ghot"
					|| song.source == "gha") {

					song.partDifficulties[Instrument.GUITAR] = -2;
					song.partDifficulties[Instrument.BASS] = -2;
				}
			}
		}

		// private static void LoadSongLengthFromAudio(SongInfo song) {
		// 	// TODO: Use BASS

		// 	// // Load file
		// 	// var songOggPath = Path.Combine(song.mainFile.FullName, "song.ogg");
		// 	// var file = TagLib.File.Create(songOggPath);

		// 	// // Save 
		// 	// song.songLength = (float) file.Properties.Duration.TotalSeconds;
		// }
	}
}