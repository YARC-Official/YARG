using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using YARG.Data;
using YARG.Serialization;
using YARG.Settings;

namespace YARG {
	public static class SongLibrary {
		private const int CACHE_VERSION = 3;
		private class SongCacheJson {
			public int version = CACHE_VERSION;
			public List<SongInfo> songs;
		}

		public static float loadPercent = 0f;

		private static string songFolderOverride = null;
		/// <value>
		/// The location of the song folder.
		/// </value>
		public static string SongFolder {
			get => songFolderOverride ?? SettingsManager.GetSettingValue<string>("songFolder");
			set => songFolderOverride = value;
		}

		/// <value>
		/// The location of the local or remote cache (depending on whether we are connected to a server).
		/// </value>
		public static string CacheFile => Path.Combine(SongFolder, "yarg_cache.json");

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

			if (File.Exists(CacheFile) || GameManager.client != null) {
				var success = ReadCache();
				if (success) {
					return true;
				}
			}

			ThreadPool.QueueUserWorkItem(_ => {
				Songs = new();

				loadPercent = 0f;
				CreateSongInfoFromFiles(new(SongFolder));
				loadPercent = 0.1f;
				ReadSongIni();
				loadPercent = 0.9f;
				CreateCache();
				loadPercent = 1f;
			});
			return false;
		}

		/// <summary>
		/// Populate <see cref="Songs"/> with <see cref="SongFolder"/> contents.<br/>
		/// This is create a basic <see cref="SongInfo"/> object for each song.<br/>
		/// We need to look at the <c>song.ini</c> files for more details.
		/// </summary>
		private static void CreateSongInfoFromFiles(DirectoryInfo songDir) {
			if (!songDir.Exists) {
				Directory.CreateDirectory(songDir.FullName);
			}

			foreach (var folder in songDir.EnumerateDirectories()) {
				if (new FileInfo(Path.Combine(folder.FullName, "song.ini")).Exists) {
					// If the folder has a song.ini or a songs.dta, it is a song folder
					Songs.Add(new SongInfo(folder));
				} else if (new FileInfo(Path.Combine(folder.FullName, "songs.dta")).Exists) {
					// Read this dir's songs.dta and add its contents to the song list
					List<SongInfo> dtaSongs = RockBandSTFS.ParseSongsDta(folder);
					foreach (SongInfo localSongInfo in dtaSongs) {
						Songs.Add(localSongInfo);
					}
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
			foreach (var song in Songs) {
				SongIni.CompleteSongInfo(song);

				// song.ini loading accounts for 80% of loading
				loadPercent += 1f / Songs.Count * 0.8f;
			}
		}

		/// <summary>
		/// Creates a cache from <see cref="Songs"/> so we don't have to read all of the <c>song.ini</c> again.<br/>
		/// <see cref="Songs"/> is expected to be populated and filled with <see cref="ReadSongIni"/>.
		/// </summary>
		private static void CreateCache() {
			var jsonObj = new SongCacheJson {
				songs = Songs
			};

			var json = JsonConvert.SerializeObject(jsonObj);
			Directory.CreateDirectory(new FileInfo(CacheFile).DirectoryName);
			File.WriteAllText(CacheFile, json);
		}

		/// <summary>
		/// Reads the song cache so we don't need to read of a the <c>song.ini</c> files.<br/>
		/// <see cref="CacheFile"/> should exist. If not, call <see cref="CreateCache"/>.
		/// </summary>
		private static bool ReadCache() {
			string json = File.ReadAllText(CacheFile);

			try {
				var jsonObj = JsonConvert.DeserializeObject<SongCacheJson>(json);
				if (jsonObj.version != CACHE_VERSION) {
					return false;
				}

				Songs = jsonObj.songs;
			} catch (JsonException) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Force reset songs. This makes the game re-scan if needed.
		/// </summary>
		public static void Reset() {
			Songs = null;
		}
	}
}