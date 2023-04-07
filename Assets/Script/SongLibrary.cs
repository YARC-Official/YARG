using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Data;
using YARG.Serialization;
using YARG.Settings;
using YARG.Util;

namespace YARG {
	public static class SongLibrary {
		private const int CACHE_VERSION = 4;
		private class SongCacheJson {
			public int version = CACHE_VERSION;
			public string folder = "";
			public List<SongInfo> songs;
		}

		public static float loadPercent = 0f;

		/// <value>
		/// The location of the song folder.
		/// </value>
		public static string[] SongFolders => SettingsManager.GetSettingValue<string[]>("songFolders");

		/// <value>
		/// The location of the local song cache folder.
		/// </value>
		public static string CacheFolder => Path.Combine(GameManager.PersistentDataPath, "caches");

		/// <value>
		/// A list of all of the playable songs.<br/>
		/// You must call <see cref="CreateSongInfoFromFiles"/> first.
		/// </value>
		public static Dictionary<string, SongInfo>.ValueCollection Songs => SongsByHash.Values;

		private static Queue<string> songFoldersToLoad = null;
		private static List<SongInfo> songsTemp = null;

		/// <value>
		/// A list of all of the playable songs, where keys are hashes.<br/>
		/// You must call <see cref="CreateSongInfoFromFiles"/> first.
		/// </value>
		public static Dictionary<string, SongInfo> SongsByHash {
			get;
			private set;
		} = null;

		/// <summary>
		/// Should be called before you access <see cref="SongsByHash"/>.
		/// </summary>
		public static bool FetchAllSongs() {
			if (!Directory.Exists(CacheFolder)) {
				Directory.CreateDirectory(CacheFolder);
			}

			SongsByHash = new();
			songFoldersToLoad = new(SongFolders);

			while (songFoldersToLoad.Count > 0) {
				if (!FetchSongs(songFoldersToLoad.Dequeue())) {
					return false;
				}
			}

			return true;
		}

		private static bool FetchSongs(string folderPath) {
			string folderHash = Utils.Hash(folderPath);
			string cachePath = Path.Combine(CacheFolder, folderHash + ".json");

			if (File.Exists(cachePath)) {
				var success = ReadCache(cachePath);
				if (success) {
					return true;
				}
			}

			ThreadPool.QueueUserWorkItem(_ => {
				try {
					songsTemp = new();

					// Find songs
					loadPercent = 0f;
					CreateSongInfoFromFiles(folderPath, new(folderPath));

					// Read song.ini and hashes
					loadPercent = 0.1f;
					ReadSongIni();
					GetSongHashes();

					// Populate SongsByHash, and create cache
					loadPercent = 0.9f;
					PopulateSongByHashes();
					CreateCache(folderPath, cachePath);

					// Load the next folder if there is one
					if (songFoldersToLoad.Count > 0) {
						FetchSongs(songFoldersToLoad.Dequeue());
					} else {
						// Done!
						loadPercent = 1f;
					}
				} catch (Exception e) {
					Debug.LogException(e);
				}
			});
			return false;
		}

		/// <summary>
		/// Populate <see cref="SongsByHash"/> with <see cref="SongFolder"/> contents.<br/>
		/// This is create a basic <see cref="SongInfo"/> object for each song.<br/>
		/// We need to look at the <c>song.ini</c> files for more details.
		/// </summary>
		private static void CreateSongInfoFromFiles(string rootFolder, DirectoryInfo songDir) {
			if (!songDir.Exists) {
				Directory.CreateDirectory(songDir.FullName);
			}

			foreach (var folder in songDir.EnumerateDirectories()) {
				if (new FileInfo(Path.Combine(folder.FullName, "song.ini")).Exists) {
					// If the folder has a song.ini, it is a song folder
					songsTemp.Add(new SongInfo(folder, rootFolder));
				} else {
					// Otherwise, treat it as a sub-folder
					CreateSongInfoFromFiles(rootFolder, folder);
				}
			}
		}

		/// <summary>
		/// Reads the <c>song.ini</c> for each <see cref="SongInfo"/> in <see cref="songsTemp"/>.<br/>
		/// <see cref="songsTemp"/> is expected to be populated.
		/// </summary>
		private static void ReadSongIni() {
			foreach (var song in songsTemp) {
				// song.ini loading accounts for 40% of loading
				loadPercent += 1f / songsTemp.Count * 0.4f;

				SongIni.CompleteSongInfo(song);
			}
		}

		/// <summary>
		/// Gets the MD5 hash for each chart in <see cref="songsTemp"/>.<br/>
		/// <see cref="songsTemp"/> is expected to be populated.
		/// </summary>
		private static void GetSongHashes() {
			foreach (var song in songsTemp) {
				// Hashing loading accounts for 40% of loading
				loadPercent += 1f / songsTemp.Count * 0.4f;

				try {
					string midFile = Path.Combine(song.folder.FullName, "notes.mid");
					string chartFile = Path.Combine(song.folder.FullName, "notes.chart");

					string chosenFile = null;

					// Get the correct file
					if (File.Exists(midFile)) {
						chosenFile = midFile;
					} else if (File.Exists(chartFile)) {
						chosenFile = chartFile;
					} else {
						Debug.LogError($"Song `{song.folder.Name}` has no notes.mid or notes.chart! Could not get hash.");
						song.hash = null;
						continue;
					}

					// MD5 checksum of the file
					using var md5 = new MD5CryptoServiceProvider();
					using var stream = File.OpenRead(chosenFile);
					var hash = md5.ComputeHash(stream);
					song.hash = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
				} catch (Exception e) {
					Debug.LogError($"Could not get hash for song `{song.folder.Name}`.");
					Debug.LogException(e);
					song.hash = null;
				}
			}
		}

		/// <summary>
		/// Takes the <see cref="songsTemp"/> and populates <see cref="SongsByHash"/>.<br/>
		/// <see cref="songsTemp"/> is expected to be populated.
		/// </summary>
		private static void PopulateSongByHashes() {
			foreach (var song in songsTemp) {
				if (song.hash == null) {
					continue;
				}

				if (SongsByHash.ContainsKey(song.hash)) {
					Debug.LogError($"Duplicate hash `{song.hash}` for songs `{SongsByHash[song.hash].folder.Name}` and `{song.folder.Name}`!");
					continue;
				}

				SongsByHash[song.hash] = song;
			}

			songsTemp = null;
		}

		/// <summary>
		/// Creates a cache from <see cref="Songs"/> so we don't have to read all of the <c>song.ini</c> again.<br/>
		/// <see cref="Songs"/> is expected to be populated and filled with <see cref="ReadSongIni"/>.
		/// </summary>
		private static void CreateCache(string root, string cachePath) {
			// Conglomerate songs by root folder
			var songCache = new List<SongInfo>();
			foreach (var song in Songs) {
				if (song.rootFolder != root) {
					continue;
				}

				songCache.Add(song);
			}

			var jsonObj = new SongCacheJson {
				folder = root,
				songs = songCache
			};

			var json = JsonConvert.SerializeObject(jsonObj);
			File.WriteAllText(cachePath, json);
		}

		/// <summary>
		/// Reads the song cache so we don't need to read of a the <c>song.ini</c> files.<br/>
		/// <see cref="CacheFile"/> should exist. If not, call <see cref="CreateCache"/>.
		/// </summary>
		private static bool ReadCache(string cacheFile) {
			string json = File.ReadAllText(cacheFile);

			try {
				var jsonObj = JsonConvert.DeserializeObject<SongCacheJson>(json);
				if (jsonObj.version != CACHE_VERSION) {
					return false;
				}

				// Combine all of the songs into one list
				foreach (var song in jsonObj.songs) {
					song.rootFolder = jsonObj.folder;

					if (SongsByHash.ContainsKey(song.hash)) {
						Debug.LogWarning($"Duplicate hash `{song.hash}` for songs `{SongsByHash[song.hash].folder.Name}` and `{song.folder.Name}`!");
						continue;
					}

					SongsByHash.Add(song.hash, song);
				}
			} catch (JsonException) {
				return false;
			}

			return true;
		}

		/// <summary>
		/// Force reset songs. This makes the game re-scan if needed.
		/// </summary>
		public static void Reset() {
			SongsByHash = null;
		}
	}
}