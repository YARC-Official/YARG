using System;
using System.IO;
using IniParser;
using IniParser.Model;
using UnityEngine;
using YARG.Data;

namespace YARG.Serialization {
	public static class SongIni {
		public static SongInfo CompleteSongInfo(SongInfo song, FileIniDataParser parser) {
			if (song.fetched) {
				return song;
			}

			var file = new FileInfo(Path.Combine(song.folder.ToString(), "song.ini"));
			if (!file.Exists) {
				return song;
			}

			song.fetched = true;
			try {
				var data = parser.ReadFile(file.FullName);

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
				song.SongName ??= section["name"];
				song.artistName ??= section["artist"];

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
					song.errored = true;
					Debug.LogError($"No song length found for `{song.folder}`.");
					return song;
				}

				// Get song delay (0 if none)
				if (section.ContainsKey("delay")) {
					int rawDelay = int.Parse(section["delay"]);
					song.delay = rawDelay / 1000f;
				} else {
					song.delay = 0f;
				}
			} catch (Exception e) {
				song.errored = true;
				Debug.LogError($"Failed to parse song.ini for `{song.folder}`.");
				Debug.LogException(e);
			}

			return song;
		}

		public static SongInfo CompleteSongInfo(SongInfo song) {
			return CompleteSongInfo(song, new FileIniDataParser());
		}
	}
}