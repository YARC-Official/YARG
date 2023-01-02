using System.Collections.Generic;
using System.IO;
using System.Threading;
using IniParser;
using Newtonsoft.Json;
using YARG.Data;
using YARG.Serialization;

namespace YARG {
	public static class SongLibrary {
		public static DirectoryInfo songFolder = new(@"B:\YARG_Songs");

		public static float loadPercent = 0f;

		/// <value>
		/// The location of the local or remote cache (depending on whether we are connected to a server).
		/// </value>
		public static FileInfo CacheFile {
			get {
				if (GameManager.client != null) {
					return GameManager.client.remoteCache;
				}

				return new(Path.Combine(songFolder.ToString(), "yarg_cache.json"));
			}
		}

		/// <value>
		/// A list of all of the playable songs.<br/>
		/// You must call <see cref="CreateSongInfoFromFiles"/> first.
		/// </value>
		public static List<SongInfo> Songs {
			get;
			private set;
		} = null;

		/// <summary>
		/// Should be called before you access <see cref="Songs"/>.
		/// </summary>
		public static bool FetchSongs() {
			if (Songs != null) {
				return true;
			}

			if (CacheFile.Exists || GameManager.client != null) {
				ReadCache();
				return true;
			} else {
				ThreadPool.QueueUserWorkItem(_ => {
					Songs = new();

					loadPercent = 0f;
					CreateSongInfoFromFiles(songFolder);
					loadPercent = 0.1f;
					ReadSongIni();
					loadPercent = 0.9f;
					CreateCache();
					loadPercent = 1f;
				});
				return false;
			}
		}

		/// <summary>
		/// Populate <see cref="Songs"/> with <see cref="songFolder"/> contents.<br/>
		/// This is create a basic <see cref="SongInfo"/> object for each song.<br/>
		/// We need to look at the <c>song.ini</c> files for more details.
		/// </summary>
		private static void CreateSongInfoFromFiles(DirectoryInfo songDir) {
			var directories = songDir.GetDirectories();

			foreach (var folder in directories) {
				if (new FileInfo(Path.Combine(folder.FullName, "song.ini")).Exists) {
					// If the folder has a song.ini, it is a song folder
					Songs.Add(new SongInfo(folder));
				} else {
					// Otherwise, treat it as a sub-folder
					CreateSongInfoFromFiles(folder);
				}
			}
		}

		/// <summary>
		/// Reads the <c>song.ini</c> for each <see cref="SongInfo"/> in <see cref="Songs"/>.<br/>
		/// <see cref="Songs"/> is expected to be populated.
		/// </summary>
		private static void ReadSongIni() {
			var parser = new FileIniDataParser();
			foreach (var song in Songs) {
				SongIni.CompleteSongInfo(song, parser);

				// song.ini loading accounts for 80% of loading
				loadPercent += 1f / Songs.Count * 0.8f;
			}
		}

		/// <summary>
		/// Creates a cache from <see cref="Songs"/> so we don't have to read all of the <c>song.ini</c> again.<br/>
		/// <see cref="Songs"/> is expected to be populated and filled with <see cref="ReadSongIni"/>.
		/// </summary>
		private static void CreateCache() {
			var json = JsonConvert.SerializeObject(Songs, Formatting.Indented);
			File.WriteAllText(CacheFile.ToString(), json.ToString());
		}

		/// <summary>
		/// Reads the song cache so we don't need to read of a the <c>song.ini</c> files.<br/>
		/// <see cref="CacheFile"/> should exist. If not, call <see cref="CreateCache"/>.
		/// </summary>
		private static void ReadCache() {
			string json = File.ReadAllText(CacheFile.ToString());
			Songs = JsonConvert.DeserializeObject<List<SongInfo>>(json);
		}
	}
}