using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YARG.Song {
	public class SongScanner : MonoBehaviour {

		/*/
		 
		 TODO: Add parallel scanning across multiple drives
		 
		 */

		private readonly Dictionary<string, SongCache> _songCaches;
		private readonly Dictionary<string, List<SongEntry>> _songsByCacheFolder;
		private readonly Dictionary<string, List<SongError>> _songErrors;

		private Thread _scanningThread;
		
		public SongScanner() {
			Debug.Log("Initialized SongScanner");
			_songCaches = new Dictionary<string, SongCache>();
			_songsByCacheFolder = new Dictionary<string, List<SongEntry>>();
			_songErrors = new Dictionary<string, List<SongError>>();
		}

		private void OnApplicationQuit() {
			if (_scanningThread is null || !_scanningThread.IsAlive) return;
			
			Debug.Log("ABORTING SONG SCAN");
			_scanningThread.Abort();
		}

		public void AddSongFolder(string path) {
			_songErrors.Add(path, new List<SongError>());
				
			if (!Directory.Exists(path)) {
				_songErrors[path].Add(new SongError(path, ScanResult.InvalidDirectory));
					
				Debug.LogError($"Invalid song directory: {path}");
				return;
			}
				
			_songsByCacheFolder.Add(path, new List<SongEntry>());
			_songCaches.Add(path, new SongCache(path));
		}

		public async UniTask<List<SongEntry>> StartScan(bool fast) {
			var songs = new List<SongEntry>();
			
			if(_songsByCacheFolder.Keys.Count == 0) {
				Debug.LogError("No song folders added to SongScanner");
				return songs;
			}

			if (fast) {
				// Only reads cache
				_scanningThread = new Thread(FastScan) {
					IsBackground = true
				};
			} else {
				// Scans directories
				_scanningThread = new Thread(FullScan) {
					IsBackground = true
				};
			}
			
			_scanningThread.Start();
			
			// Threads can have a startup time, so we wait until it's alive
			while (!_scanningThread.IsAlive) {
				await UniTask.NextFrame();
			}
			while (_scanningThread.IsAlive) {
				// Update UI here

				await UniTask.NextFrame();
			}

			_scanningThread = null;
			
			foreach(var error in _songErrors) {
				if(error.Value.Count == 0) continue;
				
				Debug.LogError($"Error for cache {error.Key}:");
				foreach(var song in error.Value) {
					Debug.LogError($"Song {song.Directory} had error {song.Result}");
				}
			}

			return songs;
		}

		private void FullScan() {
			Debug.Log("Performing full scan");
			foreach(string folder in _songsByCacheFolder.Keys) {
				Debug.Log($"Scanning folder: {folder}");
				ScanSubDirectory(folder, folder, _songsByCacheFolder[folder]);

				Debug.Log($"Finished scanning {folder}, writing cache");
				
				_songCaches[folder].WriteCache(_songsByCacheFolder[folder]);
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

		private void ScanSubDirectory(string cache, string folder, ICollection<SongEntry> songs) {
			// Raw CON folder, so don't scan anymore subdirectories here
			if (File.Exists(Path.Combine(folder, "songs", "songs.dta"))) {
				// TODO Scan raw CON
				
				return;
			}
			
			string[] subdirectories = Directory.GetDirectories(folder);
			
			foreach (string subdirectory in subdirectories) {
				ScanSubDirectory(cache, subdirectory, songs);
			}
			
			var result = ScanSong(folder, out var song);
			switch (result) {
				case ScanResult.Ok:
					songs.Add(song);
					Debug.Log("Added song: " + song.Name);
					break;
				case ScanResult.NotASong:
					break;
				default:
					_songErrors[cache].Add(new SongError(folder, result));
					break;
			}
		}

		private static ScanResult ScanSong(string directory, out SongEntry song) {
			// Usually we could infer metadata from a .chart file if no ini exists
			// But for now we just skip this song.
			song = null;
			if(!File.Exists(Path.Combine(directory, "song.ini"))) {
				return ScanResult.NotASong;
			}
			
			if(!File.Exists(Path.Combine(directory, "notes.chart")) && !File.Exists(Path.Combine(directory, "notes.mid"))) {
				return ScanResult.NoNotesFile;
			}

			if (AudioHelpers.GetSupportedStems(directory).Count == 0) {
				return ScanResult.NoAudioFile;
			}
			
			string notesFile = File.Exists(Path.Combine(directory, "notes.chart")) ? "notes.chart" : "notes.mid";
			
			// Windows has a 260 character limit for file paths, so we need to check this
			if(Path.Combine(directory, notesFile).Length >= 255) {
				return ScanResult.NoNotesFile;
			}
			
			byte[] bytes = File.ReadAllBytes(Path.Combine(directory, notesFile));
			
			var checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes)).Replace("-", "");
			
			// We have a song.ini, notes file and audio. The song is scannable.
			song = new IniSongEntry {
				Location = directory,
				Checksum = checksum,
				NotesFile = notesFile,
			};

			return ScanHelpers.ParseSongIni(Path.Combine(directory, "song.ini"), (IniSongEntry)song);
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