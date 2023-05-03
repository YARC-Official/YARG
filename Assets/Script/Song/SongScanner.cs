using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Song {
	public class SongScanner : MonoBehaviour {
		
		private const int MAX_THREAD_COUNT = 2;
		
		private readonly List<string> _songFolders;

		private SongScanThread[] _scanThreads;

		private bool _isScanning;
		
		public SongScanner() {
			Debug.Log("Initialized SongScanner");
			_songFolders = new List<string>();
			_scanThreads = new SongScanThread[MAX_THREAD_COUNT];
			
			for(int i = 0; i < _scanThreads.Length; i++) {
				_scanThreads[i] = new SongScanThread(false);
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

		public void AddSongFolder(string path) {
			_songFolders.Add(path);
		}

		public async UniTask<List<SongEntry>> StartScan(bool fast) {
			var songs = new List<SongEntry>();

			if (_songFolders.Count == 0) {
				Debug.LogError("No song folders added to SongScanner");
				return songs;
			}
			
			for(int i = 0; i < _scanThreads.Length; i++) {
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

				await UniTask.NextFrame();
			}
			
			// All threads have finished here

			foreach(var thread in _scanThreads) {
				songs.AddRange(thread.Songs);
			}

			_isScanning = false;
			Debug.Log("FINISHED SCANNING");
			
			// foreach (var error in _songErrors) {
			// 	if (error.Value.Count == 0) continue;
			//
			// 	Debug.LogError($"Error for cache {error.Key}:");
			// 	foreach (var song in error.Value) {
			// 		Debug.LogError($"Song {song.Directory} had error {song.Result}");
			// 	}
			// }

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