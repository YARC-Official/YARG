using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Song {
	public class SongScanner {

		private const int MAX_THREAD_COUNT = 2;

		private readonly ICollection<string> _songFolders;

		private SongScanThread[] _scanThreads;

		public int TotalFoldersScanned { get; private set; }
		public int TotalSongsScanned { get; private set; }
		public int TotalErrorsEncountered { get; private set; }

		private bool _isScanning;
		private bool _hasScanned;

		public SongScanner(ICollection<string> songFolders) {
			_songFolders = songFolders;

			Debug.Log("Initialized SongScanner");
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

		public async UniTask<List<SongEntry>> StartScan(bool fast, Action<SongScanner> updateUi) {
			if (_hasScanned) {
				throw new Exception("Cannot use a SongScanner after it has already scanned");
			}
			var songs = new List<SongEntry>();

			if (_songFolders.Count == 0) {
				Debug.LogError("No song folders added to SongScanner");
				return songs;
			}

			_scanThreads = new SongScanThread[MAX_THREAD_COUNT];

			for (int i = 0; i < _scanThreads.Length; i++) {
				_scanThreads[i] = new SongScanThread(fast);
			}

			AssignFoldersToThreads();

			_isScanning = true;

			foreach (var scanThread in _scanThreads) {
				scanThread.StartScan();
			}

			// Threads can have a startup time, so we wait until it's alive
			while (GetActiveThreads() == 0) {
				await UniTask.NextFrame();
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

				updateUi(this);

				await UniTask.NextFrame();
			}

			// All threads have finished here

			foreach (var thread in _scanThreads) {
				songs.AddRange(thread.Songs);
			}

			_isScanning = false;
			_hasScanned = true;
			Debug.Log("FINISHED SCANNING");

			// TODO output song errors at some point

			_scanThreads = null;

			return songs;
		}

		public int GetActiveThreads() {
			return _scanThreads.Count(thread => thread.IsScanning());
		}

		private void AssignFoldersToThreads() {
			var drives = DriveInfo.GetDrives();

			var driveFolders = drives.ToDictionary(drive => drive, _ => new List<string>());

			foreach (var folder in _songFolders) {
				var drive = drives.FirstOrDefault(d => folder.StartsWith(d.RootDirectory.Name));
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

	}

	public enum ScanResult {
		Ok,
		InvalidDirectory,
		NotASong,
		NoNotesFile,
		NoAudioFile,
	}

	public struct SongError {
		public string Directory { get; }
		public ScanResult Result { get; }

		public SongError(string directory, ScanResult result) {
			Directory = directory;
			Result = result;
		}
	}
}