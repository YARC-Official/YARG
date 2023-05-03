using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using YARG.Serialization;

namespace YARG.Song {
	public class SongScanThread {

		private Thread _thread;

		public volatile int foldersScanned;
		public volatile int songsScanned;
		public volatile int errorsEncountered;

		private int _foldersScanned;
		private int _songsScanned;
		private int _errorsEncountered;

		private readonly Dictionary<string, List<SongEntry>> _songsByCacheFolder;
		private readonly Dictionary<string, List<SongError>> _songErrors;
		private readonly Dictionary<string, SongCache>       _songCaches;

		public IReadOnlyList<SongEntry> Songs => _songsByCacheFolder.Values.SelectMany(x => x).ToList();

		public SongScanThread(bool fast) {
			_songsByCacheFolder = new Dictionary<string, List<SongEntry>>();
			_songErrors = new Dictionary<string, List<SongError>>();
			_songCaches = new Dictionary<string, SongCache>();

			_thread = fast ? new Thread(FastScan) : new Thread(FullScan);
		}

		public void AddFolder(string folder) {
			if (IsScanning()) {
				throw new Exception("Cannot add folder while scanning");
			}

			_songsByCacheFolder.Add(folder, new List<SongEntry>());
			_songErrors.Add(folder, new List<SongError>());
			_songCaches.Add(folder, new SongCache(folder));
		}

		public bool IsScanning() {
			return _thread is not null && _thread.IsAlive;
		}

		public void Abort() {
			if (_thread is null || !_thread.IsAlive) {
				return;
			}

			_thread.Abort();
			_thread = null;
		}

		public void StartScan() {
			if (_thread.IsAlive) {
				Debug.LogError("This scan thread is already in progress!");
				return;
			}

			if (_songsByCacheFolder.Keys.Count == 0) {
				Debug.LogWarning("No song folders added to this thread");
				return;
			}

			_thread.Start();
		}

		private void FullScan() {
			Debug.Log("Performing full scan");
			foreach (string cache in _songsByCacheFolder.Keys) {
				// Folder doesn't exist, so report as an error and skip
				if (!Directory.Exists(cache)) {
					_songErrors[cache].Add(new SongError(cache, ScanResult.InvalidDirectory));

					Debug.LogError($"Invalid song directory: {cache}");
					continue;
				}

				Debug.Log($"Scanning folder: {cache}");
				ScanSubDirectory(cache, cache, _songsByCacheFolder[cache]);

				Debug.Log($"Finished scanning {cache}, writing cache");

				_songCaches[cache].WriteCache(_songsByCacheFolder[cache]);
				Debug.Log("Wrote cache");
			}
		}

		private void FastScan() {
			Debug.Log("Performing fast scan");

			// This is stupid
			var caches = new Dictionary<string, List<SongEntry>>();
			foreach (string folder in _songsByCacheFolder.Keys) {
				Debug.Log($"Reading cache of {folder}");
				caches.Add(folder, _songCaches[folder].ReadCache());
				Debug.Log($"Read cache of {folder}");
			}

			foreach (var cache in caches) {
				_songsByCacheFolder[cache.Key] = cache.Value;
				Debug.Log($"Songs read from {cache.Key}: {cache.Value.Count}");
			}
		}

		private void ScanSubDirectory(string cacheFolder, string subDir, ICollection<SongEntry> songs) {
			_foldersScanned++;
			foldersScanned = _foldersScanned;

			// Raw CON folder, so don't scan anymore subdirectories here
			string dtaPath = Path.Combine(subDir, "songs", "songs.dta");
			if (File.Exists(dtaPath)) {
				var files = XboxRawfileBrowser.BrowseFolder(dtaPath, Path.Combine(dtaPath, "TODO CHANGE"));

				foreach (var file in files) {
					ScanConSong(cacheFolder, file, out var conSong);
					
					_songsScanned++;
					songsScanned = _songsScanned;
					songs.Add(conSong);
				}

				return;
			}
			
			string[] subdirectories = Directory.GetDirectories(subDir);

			foreach (string subdirectory in subdirectories) {
				ScanSubDirectory(cacheFolder, subdirectory, songs);
			}

			var result = ScanIniSong(cacheFolder, subDir, out var song);
			switch (result) {
				case ScanResult.Ok:
					_songsScanned++;
					songsScanned = _songsScanned;

					songs.Add(song);
					break;
				case ScanResult.NotASong:
					break;
				default:
					_errorsEncountered++;
					errorsEncountered = _errorsEncountered;
					_songErrors[cacheFolder].Add(new SongError(subDir, result));
					Debug.Log($"$Error encountered with {subDir}");
					break;
			}
		}

		private static ScanResult ScanIniSong(string cache, string directory, out SongEntry song) {
			// Usually we could infer metadata from a .chart file if no ini exists
			// But for now we just skip this song.
			song = null;
			if (!File.Exists(Path.Combine(directory, "song.ini"))) {
				return ScanResult.NotASong;
			}

			if (!File.Exists(Path.Combine(directory, "notes.chart")) &&
			    !File.Exists(Path.Combine(directory, "notes.mid"))) {
				return ScanResult.NoNotesFile;
			}

			if (AudioHelpers.GetSupportedStems(directory).Count == 0) {
				return ScanResult.NoAudioFile;
			}

			string notesFile = File.Exists(Path.Combine(directory, "notes.chart")) ? "notes.chart" : "notes.mid";

			// Windows has a 260 character limit for file paths, so we need to check this
			if (Path.Combine(directory, notesFile).Length >= 255) {
				return ScanResult.NoNotesFile;
			}

			byte[] bytes = File.ReadAllBytes(Path.Combine(directory, notesFile));

			var checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes)).Replace("-", "");

			// We have a song.ini, notes file and audio. The song is scannable.
			song = new IniSongEntry {
				CacheRoot = cache,
				Location = directory,
				Checksum = checksum,
				NotesFile = notesFile,
			};

			return ScanHelpers.ParseSongIni(Path.Combine(directory, "song.ini"), (IniSongEntry) song);
		}

		private static ScanResult ScanConSong(string cache, XboxSong file, out RawConSongEntry songEntry) {
			byte[] bytes = File.ReadAllBytes(Path.Combine(file.SongFolderPath, "notes.mid"));

			var checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes)).Replace("-", "");

			songEntry = new RawConSongEntry {
				CacheRoot = cache,
				Location = file.SongFolderPath,
				NotesFile = "notes.mid",
				Checksum = checksum,
			};

			file.CompleteSongInfo(songEntry, true);

			return ScanResult.Ok;
		}
	}

}