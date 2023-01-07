using System;
using System.Collections.Generic;
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
		}

		public static SongInfo CompleteSongInfo(SongInfo song) {
			if (song.fetched) {
				return song;
			}

			var file = new FileInfo(Path.Combine(song.folder.ToString(), "song.ini"));
			if (!file.Exists) {
				return song;
			}

			song.fetched = true;
			try {
				var data = PARSER.ReadFile(file.FullName, Encoding.UTF8);

				// Get song section name
				KeyDataCollection section;
				if (data.Sections.ContainsSection("song")) {
					section = data["song"];
				} else if (data.Sections.ContainsSection("Song")) {
					section = data["Song"];
				} else {
					song.errored = true;
					Debug.LogError($"No `song` section found in `{song.folder}`.");
					return song;
				}

				// Set basic info
				song.SongName = section["name"];
				song.ArtistName = section["artist"];

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
					Debug.LogWarning($"No song length found for `{song.folder}`. Loading audio file. This might take longer.");
					LoadSongLengthFromAudio(song);
				}

				// Get song delay (0 if none)
				if (section.ContainsKey("delay")) {
					int rawDelay = int.Parse(section["delay"]);
					song.delay = rawDelay / 1000f;
				} else {
					song.delay = 0f;
				}

				// Get difficulties
				foreach (var kvp in new Dictionary<string, int>(song.partDifficulties)) {
					var key = "diff_" + kvp.Key;
					if (section.ContainsKey(key)) {
						song.partDifficulties[kvp.Key] = int.Parse(section[key]);
					}
				}
			} catch (Exception e) {
				song.errored = true;
				Debug.LogError($"Failed to parse song.ini for `{song.folder}`.");
				Debug.LogException(e);
			}

			return song;
		}

		private static void LoadSongLengthFromAudio(SongInfo song) {
			// Load file
			var songOggPath = Path.Combine(song.folder.FullName, "song.ogg");
			var file = TagLib.File.Create(songOggPath);

			// Save 
			song.songLength = (float) file.Properties.Duration.TotalSeconds;
		}
	}
}