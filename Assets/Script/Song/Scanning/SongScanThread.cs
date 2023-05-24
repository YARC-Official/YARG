using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using UnityEngine;
using DtxCS.DataTypes;
using XboxSTFS;
using YARG.Serialization;
using YARG.Song.Preparsers;

namespace YARG.Song {
	public class SongScanThread {

		private Thread _thread;

		public volatile int foldersScanned;
		public volatile int songsScanned;
		public volatile int errorsEncountered;

		private int _foldersScanned;
		private int _songsScanned;
		private int _errorsEncountered;

		private string _updateFolderPath = string.Empty;
		private Dictionary<string, List<DataArray>> _songUpdateDict = new();

		private string _upgradeFolderPath = string.Empty;
		private List<XboxSTFSFile> _conFiles;
		private List<(SongProUpgrade, DataArray)> _songUpgradeDict = new();

		private readonly Dictionary<string, List<SongEntry>> _songsByCacheFolder;
		private readonly Dictionary<string, List<SongError>> _songErrors;
		private readonly Dictionary<string, SongCache>       _songCaches;
		private readonly List<string>                        _cacheErrors;

		public IReadOnlyList<SongEntry> Songs => _songsByCacheFolder.Values.SelectMany(x => x).ToList();
		public IReadOnlyDictionary<string, List<SongError>> SongErrors => _songErrors;
		public IReadOnlyList<string> CacheErrors => _cacheErrors;

		public SongScanThread(bool fast) {
			_songsByCacheFolder = new Dictionary<string, List<SongEntry>>();
			_songErrors = new Dictionary<string, List<SongError>>();
			_songCaches = new Dictionary<string, SongCache>();
			_cacheErrors = new List<string>();
			_conFiles = new List<XboxSTFSFile>();

			_thread = fast ? new Thread(FastScan) : new Thread(FullScan);
		}

		public void AddFolder(string folder) {
			if (IsScanning()) {
				throw new Exception("Cannot add folder while scanning");
			}

			if (_songsByCacheFolder.ContainsKey(folder)) {
				Debug.LogWarning("Two song folders with same directory!");
				return;
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

		public bool StartScan() {
			if (_thread.IsAlive) {
				Debug.LogError("This scan thread is already in progress!");
				return false;
			}

			if (_songsByCacheFolder.Keys.Count == 0) {
				Debug.LogWarning("No song folders added to this thread");
				return false;
			}

			_thread.Start();
			return true;
		}

		private void FullScan() {
			Debug.Log("Performing full scan");
			foreach (string cache in _songsByCacheFolder.Keys) {
				// Folder doesn't exist, so report as an error and skip
				if (!Directory.Exists(cache)) {
					_songErrors[cache].Add(new SongError(cache, ScanResult.InvalidDirectory, ""));

					Debug.LogError($"Invalid song directory: {cache}");
					continue;
				}

				Debug.Log($"Scanning folder: {cache}");
				ScanSubDirectory(cache, cache, _songsByCacheFolder[cache]);

				Debug.Log($"Finished scanning {cache}, writing cache");

				_songCaches[cache].WriteCache(_songsByCacheFolder[cache], _conFiles);
				Debug.Log("Wrote cache");
			}
		}

		private void FastScan() {
			Debug.Log("Performing fast scan");

			// This is stupid
			var caches = new Dictionary<string, List<SongEntry>>();
			foreach (string folder in _songsByCacheFolder.Keys) {
				try {
					Debug.Log($"Reading cache of {folder}");
					var cacheScan = _songCaches[folder].ReadCache();
					caches.Add(folder, cacheScan.Item1);
					_conFiles.AddRange(cacheScan.Item2);
					Debug.Log($"Read cache of {folder}");
				} catch (Exception e) {
					_cacheErrors.Add(folder);

					Debug.LogException(e);
					Debug.LogError($"Failed to read cache of {folder}");
				}
			}

			foreach (var cache in caches) {
				_songsByCacheFolder[cache.Key] = cache.Value;
				Debug.Log($"Songs read from {cache.Key}: {cache.Value.Count}");
			}
		}

		private void ScanSubDirectory(string cacheFolder, string subDir, ICollection<SongEntry> songs) {
			_foldersScanned++;
			foldersScanned = _foldersScanned;

			// scan the songs_updates folder at the root before base song scanning
			string updatePath = Path.Combine(subDir, "songs_updates");
			if (Directory.Exists(updatePath)) {
				if (_updateFolderPath == string.Empty) {
					_updateFolderPath = updatePath;
					Debug.Log($"Song updates found at {_updateFolderPath}");
					_songUpdateDict = XboxSongUpdateBrowser.FetchSongUpdates(_updateFolderPath);
					Debug.Log($"Total count of song updates found: {_songUpdateDict.Count}");
				}
			}

			// scan the songs_upgrades folder at the root before base song scanning
			string upgradePath = Path.Combine(subDir, "songs_upgrades");
			if(Directory.Exists(upgradePath)){
				if(_upgradeFolderPath == string.Empty){
					_upgradeFolderPath = upgradePath;
					Debug.Log($"Song upgrades found at {_upgradeFolderPath}");
					// first, parse the raw upgrades. then, parse all the upgrades contained within CONs
					_songUpgradeDict = XboxSongUpgradeBrowser.FetchSongUpgrades(_upgradeFolderPath, ref _conFiles);
					Debug.Log($"Total count of song upgrades found: {_songUpgradeDict.Count}");
				}
			}

			// Check if it is a song folder
			var result = ScanIniSong(cacheFolder, subDir, out var song);
			switch (result) {
				case ScanResult.Ok:
					_songsScanned++;
					songsScanned = _songsScanned;

					songs.Add(song);
					return;
				case ScanResult.NotASong:
					break;
				default:
					_errorsEncountered++;
					errorsEncountered = _errorsEncountered;
					_songErrors[cacheFolder].Add(new SongError(subDir, result, ""));
					Debug.LogWarning($"Error encountered with {subDir}: {result}");
					return;
			}

			// Raw CON folder, so don't scan anymore subdirectories here
			if (File.Exists(Path.Combine(subDir, "songs", "songs.dta"))) {
				List<ExtractedConSongEntry> files = ExCONBrowser.BrowseFolder(subDir, _updateFolderPath, _songUpdateDict, _songUpgradeDict);
				foreach (ExtractedConSongEntry file in files) {
					// validate that the song is good to add in-game
					var ExCONResult = ScanConSong(cacheFolder, file);
					switch (ExCONResult) {
						case ScanResult.Ok:
							_songsScanned++;
							songsScanned = _songsScanned;
							songs.Add(file);
							break;
						case ScanResult.NotASong:
							break;
						default:
							_errorsEncountered++;
							errorsEncountered = _errorsEncountered;
							_songErrors[cacheFolder].Add(new SongError(subDir, ExCONResult, file.Name));
							Debug.LogWarning($"Error encountered with {subDir}");
							break;
					}
				}

				return;
			}

			// Iterate through the files in this current directory to look for CON files
			try { // try-catch to prevent crash if user doesn't have permission to access a folder
				foreach (var filename in Directory.EnumerateFiles(subDir)) {
					XboxSTFSFile conFile = new();
					if (!conFile.Load(filename))
						continue;

					List<ConSongEntry> files = XboxCONFileBrowser.BrowseCON(conFile, _updateFolderPath, _songUpdateDict, _songUpgradeDict);
					if (files == null)
						continue;

					foreach (ConSongEntry file in files) {
						// validate that the song is good to add in-game
						var CONResult = ScanConSong(cacheFolder, file);
						switch (CONResult) {
							case ScanResult.Ok:
								_songsScanned++;
								songsScanned = _songsScanned;
								songs.Add(file);
								break;
							case ScanResult.NotASong:
								break;
							default:
								_errorsEncountered++;
								errorsEncountered = _errorsEncountered;
								_songErrors[cacheFolder].Add(new SongError(subDir, CONResult, file.Name));
								Debug.LogWarning($"Error encountered with {subDir}");
								break;
						}
					}

					if (files.Count > 0)
						_conFiles.Add(conFile);
				}
				string[] subdirectories = Directory.GetDirectories(subDir);
				foreach (string subdirectory in subdirectories) {
					if (subdirectory != _updateFolderPath && subdirectory != _upgradeFolderPath) {
						ScanSubDirectory(cacheFolder, subdirectory, songs);
					}
				}
			} catch (Exception e) {
				Debug.LogException(e);
			}
		}

		private static ScanResult ScanIniSong(string cache, string directory, out IniSongEntry song) {
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

			string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes)).Replace("-", "");

			var tracks = ulong.MaxValue;

			if (notesFile.EndsWith(".mid")) {
				if (!MidPreparser.GetAvailableTracks(bytes, out ulong availTracks)) {
					return ScanResult.CorruptedNotesFile;
				}
				tracks = availTracks;
			} else if (notesFile.EndsWith(".chart")) {
				if (!ChartPreparser.GetAvailableTracks(bytes, out ulong availTracks)) {
					return ScanResult.CorruptedNotesFile;
				}
				tracks = availTracks;
			}

			song = new IniSongEntry(cache, directory, checksum, notesFile, tracks);
			return song.ParseIni();
		}

		private static ScanResult ScanConSong(string cache, ExtractedConSongEntry file) {
			// Skip if the song doesn't have notes
			if (!file.ValidateMidiFile()) {
				return ScanResult.NoNotesFile;
			}

			// Skip if this is a "fake song" (tutorials, etc.)
			if (file.IsFake) {
				return ScanResult.NotASong;
			}

			try {
				if (!file.IsMoggUnencrypted())
					return ScanResult.EncryptedMogg;
			} catch {
				return ScanResult.NoAudioFile;
			}

			// all good - go ahead and build the cache info
			List<byte> bytes = new List<byte>();
			ulong tracks;

			// add base midi
			byte[] notes = file.LoadMidiFile();
			bytes.AddRange(notes);
			if (!MidPreparser.GetAvailableTracks(notes, out ulong base_tracks)) {
				return ScanResult.CorruptedNotesFile;
			}
			tracks = base_tracks;
			// add update midi, if it exists
			if (file.DiscUpdate) {
				byte[] update = file.LoadMidiUpdateFile();
				bytes.AddRange(update);
				if (!MidPreparser.GetAvailableTracks(update, out ulong update_tracks)) {
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= update_tracks;
			}
			// add upgrade midi, if it exists
			if (file.SongUpgrade != null) {
				byte[] upgrade_midi = file.SongUpgrade.GetUpgradeMidi();
				bytes.AddRange(upgrade_midi);
				if(!MidPreparser.GetAvailableTracks(upgrade_midi, out ulong upgrade_tracks)){
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= upgrade_tracks;
			}

			string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes.ToArray())).Replace("-", "");
			file.FinishScan(cache, checksum, tracks);
			return ScanResult.Ok;
		}
	}

}
