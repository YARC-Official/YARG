using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using YARG.Data;
using YARG.Serialization;
using YARG.Settings;

namespace YARG {
	public static class SongLibrary {
		private const int CACHE_VERSION = 5;
		private class SongCacheJson {
			public int version = CACHE_VERSION;
			public string folder = "";
			public List<SongInfo> songs;
		}

		private struct SongPathInfo {
			public SongInfo.SongType type;
			public string path;
			public string root;
		}

		public static string currentTaskDescription = "";
		public static bool currentlyLoading = false;
		public static float loadPercent = 0f;

		/// <value>
		/// The location(s) of the song folder(s).
		/// </value>
		public static string[] SongFolders {
			get => SettingsManager.Settings.SongFolders;
			set => SettingsManager.Settings.SongFolders = value;
		}

		/// <value>
		/// The location(s) of the song upgrade folder(s).
		/// </value>
		public static string[] SongUpgradeFolders {
			get => SettingsManager.Settings.SongUpgradeFolders;
			set => SettingsManager.Settings.SongUpgradeFolders = value;
		}

		/// <value>
		/// The location of the local song cache folder.
		/// </value>
		public static string CacheFolder => Path.Combine(GameManager.PersistentDataPath, "caches");

		/// <value>
		/// The location of the local sources file.
		/// </value>
		public static string SourcesFile => Path.Combine(GameManager.PersistentDataPath, "sources.txt");

		/// <value>
		/// The URL of the Clone Hero sources list.
		/// </value>
		public const string SOURCES_URL = "https://sources.clonehero.net/sources.txt";

		/// <value>
		/// A list of all of the playable songs.<br/>
		/// You must call <see cref="FetchAllSongs"/> first.
		/// </value>
		public static Dictionary<string, SongInfo>.ValueCollection Songs => SongsByHash.Values;

		private static Queue<string> songFoldersToLoad = null;
		private static List<SongPathInfo> songPaths = null;
		private static List<SongInfo> songsTemp = null;

		/// <value>
		/// A list of all of the playable songs, where keys are hashes.<br/>
		/// You must call <see cref="FetchAllSongs"/> first.
		/// </value>
		public static Dictionary<string, SongInfo> SongsByHash {
			get;
			private set;
		} = null;

		/// <value>
		/// A list of all of the playable songs, where keys are hashes.<br/>
		/// You must call <see cref="FetchSongSources"/> first.
		/// </value>
		public static Dictionary<string, string> SourceNames {
			get;
			private set;
		} = null;

		private static void FindSongs(string rootFolder, DirectoryInfo songDir) {
			if (!songDir.Exists) {
				Directory.CreateDirectory(songDir.FullName);
			}

			foreach (var folder in songDir.EnumerateDirectories()) {
				if (File.Exists(Path.Combine(folder.FullName, "song.ini"))) {
					// If the folder has a song.ini, it is a song folder
					songPaths.Add(new SongPathInfo {
						type = SongInfo.SongType.SONG_INI,
						path = folder.FullName,
						root = rootFolder
					});
					//Debug.Log($"Found song.ini song: {folder.FullName}");
				} else if (File.Exists(Path.Combine(folder.FullName, "songs/songs.dta"))) {
					// If the folder has a songs/songs.dta, it's a Rock Band con 
					songPaths.Add(new SongPathInfo {
						type = SongInfo.SongType.RB_CON_RAW,
						path = Path.Combine(folder.FullName, "songs"),
						root = rootFolder
					});
					//Debug.Log($"Found RB con song(s): {folder.FullName}");
				} else {
					// Scan files in folder for potential CONs
					bool isCONFolder = false;
					byte[] header = new byte[4];
					foreach (var f in folder.GetFiles()) {
						var fs = f.Open(FileMode.Open, FileAccess.Read, FileShare.Read);
						int n = fs.Read(header, 0, 4);
						string headerStr = Encoding.UTF8.GetString(header);
						if (headerStr == "CON " || headerStr == "LIVE") {
							songPaths.Add(new SongPathInfo {
								type = SongInfo.SongType.RB_CON,
								path = f.FullName,
								root = rootFolder
							});
							isCONFolder = true;
						}
					}
					// If no CONs were found, treat it as a sub-folder
					if (!isCONFolder) FindSongs(rootFolder, folder);
				}
			}
		}

		private static void ReadSongInfo() {
			foreach (var info in songPaths) {
				// Song info loading accounts for 40% of loading
				loadPercent += 1f / songPaths.Count * 0.4f;

				if (info.type == SongInfo.SongType.SONG_INI) {
					// song.ini

					try {
						// See if .mid file exists
						var midPath = Path.Combine(info.path, "notes.mid");
						var chartPath = Path.Combine(info.path, "notes.chart");

						// Create SongInfo
						SongInfo songInfo;

						// Load midi
						if (File.Exists(midPath) && !File.Exists(chartPath)) {
							songInfo = new SongInfo(midPath, info.root, info.type);
							SongIni.CompleteSongInfo(songInfo);
						} else if (File.Exists(chartPath) && !File.Exists(midPath)) {
							// Load chart

							songInfo = new SongInfo(chartPath, info.root, info.type);
							SongIni.CompleteSongInfo(songInfo);
						} else if (!File.Exists(midPath) && !File.Exists(chartPath)) {
							Debug.LogError($"`{info.path}` does not have a `notes.mid` or `notes.chart` file. Skipping.");
							continue;
						} else {
							// Both exist?

							// choose midi lol
							songInfo = new SongInfo(midPath, info.root, info.type);
							SongIni.CompleteSongInfo(songInfo);
						}

						// Add it to the list of songs
						songsTemp.Add(songInfo);
					} catch (Exception e) {
						Debug.LogError($"Error while reading song info for `{info.path}`: {e}");
					}
				} else if (info.type == SongInfo.SongType.RB_CON_RAW) {
					// Rock Band CON rawfiles

					// Read all of the songs in the file
					// Apply any song updates, if they exist
					// Debug.Log($"song path root: {info.root}");
					var files = XboxRawfileBrowser.BrowseFolder(info.path, Path.Combine(info.root, "songs_updates"));

					// Convert each to a SongInfo
					foreach (var file in files) {
						try {
							// Create a SongInfo
							var songInfo = new SongInfo(file.MidiFile, info.root, info.type);
							file.CompleteSongInfo(songInfo, true);

							// Add it to the list of songs
							songsTemp.Add(songInfo);
						} catch (Exception e) {
							Debug.LogError($"Error reading song info for `{file.MidiFile}`: {e}");
						}
					}
				} else if (info.type == SongInfo.SongType.RB_CON) {
					Debug.Log($"RB CON file: {info.path} in dir {info.root}");
					// Rock Band unextracted CON

					// Read all of the songs in the CON file
					var songInfo = XboxCONFileBrowser.BrowseCON(info.path);
					// ^this returns a List<XboxCONSong>
					// TODO: add each XboxCONSong to the list of scanned songs
				}
			}

			songPaths = null;
		}

		/// <returns>
		/// A unique hash for <paramref name="path"/>.
		/// </returns>
		public static string HashFilePath(string path) {
			return Hash128.Compute(path).ToString();
		}

		private static bool FetchSources() {
			currentTaskDescription = "Fetching list of sources.";
			Debug.Log(currentTaskDescription);

			try {
				// Retrieve sources file
				var request = WebRequest.Create(SOURCES_URL);
				request.UseDefaultCredentials = true;
				request.Timeout = 5000;
				using var response = request.GetResponse();
				using var responseReader = new StreamReader(response.GetResponseStream());

				// Store sources locally and load them
				string text = responseReader.ReadToEnd();
				File.WriteAllText(SourcesFile, text);
			} catch (Exception e) {
				Debug.LogException(e);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Reads the locally-cached sources file.<br/>
		/// Populates <see cref="SourceNames"/>
		/// </summary>
		private static bool ReadSources() {
			if (!File.Exists(SourcesFile)) {
				return false;
			}

			SourceNames ??= new();
			SourceNames.Clear();
			var sources = File.ReadAllText(SourcesFile).Split("\n");
			foreach (string source in sources) {
				if (string.IsNullOrWhiteSpace(source)) {
					continue;
				}

				// The sources are formatted as follows:
				// iconName '=' Display Name
				var pair = source.Split("'='", 2);
				if (pair.Length < 2) {
					Debug.LogWarning($"Invalid source entry when reading sources: {source}");
					continue;
				}
				SourceNames.Add(pair[0].Trim(), pair[1].Trim());
			}

			return true;
		}
	}
}