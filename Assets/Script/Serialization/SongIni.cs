using System;
using System.IO;
using IniParser;
using UnityEngine;
using YARG.UI;

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

				// Set basic info
				song.SongName ??= data["song"]["name"];
				song.artistName ??= data["song"]["artist"];
				song.source ??= data["song"]["icon"];

				// Get song length
				if (data["song"].ContainsKey("song_length")) {
					int rawLength = int.Parse(data["song"]["song_length"]);
					song.songLength = rawLength / 1000f;
				} else {
					Debug.LogWarning($"No song length found for: {song.folder}");
				}

				// Get song delay (0 if none)
				if (data["song"].ContainsKey("delay")) {
					int rawDelay = int.Parse(data["song"]["delay"]);
					song.delay = rawDelay / 1000f;
				} else {
					song.delay = 0f;
				}
			} catch (Exception e) {
				song.errored = true;
				Debug.LogException(e);
			}

			return song;
		}

		public static SongInfo CompleteSongInfo(SongInfo song) {
			return CompleteSongInfo(song, new FileIniDataParser());
		}
	}
}