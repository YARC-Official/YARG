using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;
using YARG.Util;

namespace YARG.Song {
	public enum ScanResult {
		Ok,
		InvalidDirectory,
		NotASong,
		NoNotesFile,
		NoAudioFile,
		EncryptedMogg,
		CorruptedNotesFile,
		CorruptedMetadataFile
	}

	public readonly struct SongError {
		public string Directory { get; }
		public ScanResult Result { get; }
		public string FileName { get; }

		public SongError(string directory, ScanResult result, string fileName) {
			Directory = directory;
			Result = result;
			FileName = fileName;
		}
	}

	public readonly struct ScanOutput {
		public List<SongEntry> SongEntries { get; }
		public List<CacheFolder> ErroredCaches { get; }

		public ScanOutput(List<SongEntry> songEntries, List<CacheFolder> erroredCaches) {
			SongEntries = songEntries;
			ErroredCaches = erroredCaches;
		}
	}

	public class SongScanner {
		private const int MAX_THREAD_COUNT = 2;

		private readonly List<CacheFolder> _songFolders;

		private SongScanThread[] _scanThreads;

		public int TotalFoldersScanned { get; private set; }
		public int TotalSongsScanned { get; private set; }
		public int TotalErrorsEncountered { get; private set; }

		private bool _isScanning;
		private bool _hasScanned;

		public SongScanner(IEnumerable<CacheFolder> songFolders) {
			_songFolders = songFolders.ToList();
		}

		public SongScanner(IEnumerable<string> songFolders, IEnumerable<string> portableFolders = null) {
			_songFolders = new();

			_songFolders.AddRange(songFolders.Select(i => new CacheFolder(i, false)));

			if (portableFolders is not null) {
				_songFolders.AddRange(portableFolders.Select(i => new CacheFolder(i, true)));
			}
		}

		private void OnDestroy() {
			if (_isScanning) {
				Debug.Log("ABORTING SONG SCAN");
				_isScanning = false;
			}

			if (_scanThreads == null) {
				return;
			}

			foreach (var thread in _scanThreads) {
				thread?.Abort();
			}
		}

		public async UniTask<ScanOutput> StartScan(bool fast, Action<SongScanner> updateUi) {
			if (_hasScanned) {
				throw new Exception("Cannot use a SongScanner after it has already scanned");
			}

			var songs = new List<SongEntry>();
			var cacheErrors = new List<CacheFolder>();

			if (_songFolders.Count == 0) {
				Debug.LogWarning("No song folders added to SongScanner. Returning");
				return new ScanOutput(songs, cacheErrors);
			}

			_scanThreads = new SongScanThread[MAX_THREAD_COUNT];

			for (int i = 0; i < _scanThreads.Length; i++) {
				_scanThreads[i] = new SongScanThread(fast);
			}

			AssignFoldersToThreads();

			_isScanning = true;

			// Start all threads, count skips
			int skips = 0;
			foreach (var scanThread in _scanThreads) {
				var notSkipped = scanThread.StartScan();
				if (!notSkipped) {
					skips++;
				}
			}

			// Threads can have a startup time, so we wait until it's alive
			// Don't wait if all threads skipped
			if (skips < _scanThreads.Length) {
				while (GetActiveThreads() == 0) {
					await UniTask.NextFrame();
				}
			}

			// Keep looping until all threads are done
			while (GetActiveThreads() > 0) {
				// Update UI here
				TotalFoldersScanned = 0;
				TotalSongsScanned = 0;
				TotalErrorsEncountered = 0;

				foreach (var thread in _scanThreads) {
					TotalFoldersScanned += thread.foldersScanned;
					TotalSongsScanned += thread.songsScanned;
					TotalErrorsEncountered += thread.errorsEncountered;
				}

				updateUi?.Invoke(this);

				await UniTask.NextFrame();
			}

			// All threads have finished here

			foreach (var thread in _scanThreads) {
				songs.AddRange(thread.Songs);
				cacheErrors.AddRange(thread.CacheErrors);
			}

			_isScanning = false;
			_hasScanned = true;
			Debug.Log("Finished Scanning.");

			if (!fast) {
				Debug.Log("Writing badsongs.txt");
				await WriteBadSongs();
				Debug.Log("Finished writing badsongs.txt");
			}

			_scanThreads = null;

			return new ScanOutput(songs, cacheErrors);
		}

		public int GetActiveThreads() {
			return _scanThreads.Count(thread => thread.IsScanning());
		}

		private void AssignFoldersToThreads() {
			var drives = DriveInfo.GetDrives();

			var driveFolders = drives.ToDictionary(drive => drive, _ => new List<CacheFolder>());

			foreach (var folder in _songFolders) {
				if (string.IsNullOrEmpty(folder.Folder)) {
					Debug.LogWarning("Song folder is null/empty. This is a problem with the settings menu!");
					continue;
				}

				var drive = drives.FirstOrDefault(d => folder.Folder.StartsWith(d.RootDirectory.Name));
				if (drive == null) {
					Debug.LogError($"Folder {folder} is not on a drive");
					continue;
				}

				driveFolders[drive].Add(folder);
			}

			int threadIndex = 0;
			using var enumerator = driveFolders.GetEnumerator();

			while (enumerator.MoveNext() && enumerator.Current.Key is not null) {
				// No folders for this drive
				if (enumerator.Current.Value.Count == 0) {
					continue;
				}

				// Add every folder from this drive to the thread
				enumerator.Current.Value.ForEach(x => _scanThreads[threadIndex].AddFolder(x));

				threadIndex++;

				if (threadIndex >= MAX_THREAD_COUNT) {
					threadIndex = 0;
				}
			}
		}

		private async UniTask WriteBadSongs() {
#if UNITY_EDITOR
			string badSongsPath = Path.Combine(PathHelper.PersistentDataPath, "badsongs.txt");
#else
			string badSongsPath = Path.Combine(PathHelper.ExecutablePath, "badsongs.txt");
#endif

			await using var stream = new FileStream(badSongsPath, FileMode.Create, FileAccess.Write);
			await using var writer = new StreamWriter(stream);

			foreach (var thread in _scanThreads) {
				foreach (var folder in thread.SongErrors) {
					if (folder.Value.Count == 0) {
						continue;
					}

					await writer.WriteLineAsync(folder.Key);
					folder.Value.Sort((x, y) => x.Result.CompareTo(y.Result));

					var lastResult = ScanResult.Ok;
					foreach (var error in folder.Value) {
						if (error.Result != lastResult) {
							switch (error.Result) {
								case ScanResult.InvalidDirectory:
									await writer.WriteLineAsync(
										"These songs are not in a valid directory! (Or the directory path is too long)");
									break;
								case ScanResult.NoAudioFile:
									await writer.WriteLineAsync("These songs contain no valid audio files!");
									break;
								case ScanResult.NoNotesFile:
									await writer.WriteLineAsync("These songs contain no valid notes file! (notes.chart/notes.mid)");
									break;
								case ScanResult.EncryptedMogg:
									await writer.WriteLineAsync("These songs contain encrypted moggs!");
									break;
								case ScanResult.CorruptedNotesFile:
									await writer.WriteLineAsync("These songs contain a corrupted notes.chart/notes.mid file!");
									break;
							}
							lastResult = error.Result;
						}
						await writer.WriteLineAsync($"    {error.Directory}\\{error.FileName}");
					}

					await writer.WriteLineAsync();
				}
			}
		}

	}
}