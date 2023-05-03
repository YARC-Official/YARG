using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace YARG.Song {
	public class SongScanThread {

		private Thread _thread;
		
		private readonly Dictionary<string, List<SongEntry>> _songsByCacheFolder;
		private readonly Dictionary<string, List<SongError>> _songErrors;
		private readonly Dictionary<string, SongCache> _songCaches;
		
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
			if(_thread.IsAlive) {
				Debug.LogError("This scan thread is already in progress!");
				return;
			}
			
			if(_songsByCacheFolder.Keys.Count == 0) {
				Debug.LogWarning("No song folders added to this thread");
				return;
			}
			
			_thread.Start();
		}
		
		private void FullScan() {
			Debug.Log("Performing full scan");
			foreach (string folder in _songsByCacheFolder.Keys) {
				// Folder doesn't exist, so report as an error and skip
				if (!Directory.Exists(folder)) {
					_songErrors[folder].Add(new SongError(folder, ScanResult.InvalidDirectory));
			
					Debug.LogError($"Invalid song directory: {folder}");
					continue;
				}

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
		
		private void ScanSubDirectory(string songFolder, string subdir, ICollection<SongEntry> songs) {
			// Raw CON folder, so don't scan anymore subdirectories here
			if (File.Exists(Path.Combine(subdir, "songs", "songs.dta"))) {
				// TODO Scan raw CON
				
				return;
			}
			
			string[] subdirectories = Directory.GetDirectories(subdir);
			
			foreach (string subdirectory in subdirectories) {
				ScanSubDirectory(songFolder, subdirectory, songs);
			}
			
			var result = ScanSong(subdir, out var song);
			switch (result) {
				case ScanResult.Ok:
					songs.Add(song);
					Debug.Log($"({songFolder}) Added song: " + song.Name);
					break;
				case ScanResult.NotASong:
					break;
				default:
					_songErrors[songFolder].Add(new SongError(subdir, result));
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
	
}