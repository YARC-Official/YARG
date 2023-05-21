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
using static XboxSTFS.XboxSTFSParser;
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
		private Dictionary<SongProUpgrade, DataArray> _songUpgradeDict = new();

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

				_songCaches[cache].WriteCache(_songsByCacheFolder[cache]);
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
					caches.Add(folder, _songCaches[folder].ReadCache());
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
					_songUpgradeDict = XboxSongUpgradeBrowser.FetchSongUpgrades(_upgradeFolderPath);
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
				List<ExtractedConSongEntry> files = ExCONBrowser.BrowseFolder(subDir, 
					_updateFolderPath, _songUpdateDict, _songUpgradeDict);

				foreach (ExtractedConSongEntry file in files) {
					// validate that the song is good to add in-game
					var ExCONResult = ScanExConSong(cacheFolder, file);
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
				foreach (var file in Directory.EnumerateFiles(subDir)) {
					// for each file found, read first 4 bytes and check for "CON " or "LIVE"
					using var fs = new FileStream(file, FileMode.Open, FileAccess.Read);
					using var br = new BinaryReader(fs);
					string fHeader = Encoding.UTF8.GetString(br.ReadBytes(4));
					if (fHeader == "CON " || fHeader == "LIVE") {
						List<ConSongEntry> SongsInsideCON = XboxCONFileBrowser.BrowseCON(file, 
							_updateFolderPath, _songUpdateDict, _songUpgradeDict);
						// for each CON song that was found (assuming some WERE found)
						if (SongsInsideCON != null) {
							foreach (ConSongEntry SongInsideCON in SongsInsideCON) {
								// validate that the song is good to add in-game
								var CONResult = ScanConSong(cacheFolder, SongInsideCON);
								switch (CONResult) {
									case ScanResult.Ok:
										_songsScanned++;
										songsScanned = _songsScanned;
										songs.Add(SongInsideCON);
										break;
									case ScanResult.NotASong:
										break;
									default:
										_errorsEncountered++;
										errorsEncountered = _errorsEncountered;
										_songErrors[cacheFolder].Add(new SongError(subDir, CONResult, SongInsideCON.Name));
										Debug.LogWarning($"Error encountered with {subDir}");
										break;
								}
							}
						}
					}
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

			// We have a song.ini, notes file and audio. The song is scannable.
			song = new IniSongEntry {
				CacheRoot = cache,
				Location = directory,
				Checksum = checksum,
				NotesFile = notesFile,
				AvailableParts = tracks,
			};

			return ScanHelpers.ParseSongIni(Path.Combine(directory, "song.ini"), (IniSongEntry) song);
		}

		private static ScanResult ScanExConSong(string cache, ExtractedConSongEntry file) {
			// Skip if the song doesn't have notes
			if (!File.Exists(file.NotesFile)) {
				return ScanResult.NoNotesFile;
			}

			// Skip if this is a "fake song" (tutorials, etc.)
			if (file.IsFake) {
				return ScanResult.NotASong;
			}

			// Skip if the mogg is encrypted
			if (file.MoggHeader != 0xA) {
				return ScanResult.EncryptedMogg;
			}

			// all good - go ahead and build the cache info
			List<byte> bytes = new List<byte>();
			ulong tracks;

			// add base midi
			bytes.AddRange(File.ReadAllBytes(file.NotesFile));
			if (!MidPreparser.GetAvailableTracks(File.ReadAllBytes(file.NotesFile), out ulong base_tracks)) {
				return ScanResult.CorruptedNotesFile;
			}
			tracks = base_tracks;
			// add update midi, if it exists
			if (file.DiscUpdate) {
				bytes.AddRange(File.ReadAllBytes(file.UpdateMidiPath));
				if (!MidPreparser.GetAvailableTracks(File.ReadAllBytes(file.UpdateMidiPath), out ulong update_tracks)) {
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= update_tracks;
			}
			// add upgrade midi, if it exists
			if(!string.IsNullOrEmpty(file.SongUpgrade.UpgradeMidiPath)){
				var upgrade_midi = file.SongUpgrade.GetUpgradeMidi();
				bytes.AddRange(upgrade_midi);
				if(!MidPreparser.GetAvailableTracks(upgrade_midi, out ulong upgrade_tracks)){
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= upgrade_tracks;
			}

			string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes.ToArray())).Replace("-", "");

			file.CacheRoot = cache;
			file.Checksum = checksum;
			file.AvailableParts = tracks;

			return ScanResult.Ok;
		}

		private static ScanResult ScanConSong(string cache, ConSongEntry file) {
			// Skip if the song doesn't have notes
			if(file.FLMidi == null) {
				return ScanResult.NoNotesFile;
			}

			// Skip if this is a "fake song" (tutorials, etc.)
			if (file.IsFake) {
				return ScanResult.NotASong;
			}

			// Skip if the mogg is encrypted
			if (file.MoggHeader != 0xA) {
				return ScanResult.EncryptedMogg;
			}

			// all good - go ahead and build the cache info
			List<byte> bytes = new List<byte>();
			ulong tracks;

			// add base midi
			bytes.AddRange(XboxSTFSParser.GetFile(file.Location, file.FLMidi));
			if (!MidPreparser.GetAvailableTracks(bytes.ToArray(), out ulong base_tracks)) {
				return ScanResult.CorruptedNotesFile;
			}
			tracks = base_tracks;
			// add update midi, if it exists
			if (file.DiscUpdate) {
				bytes.AddRange(File.ReadAllBytes(file.UpdateMidiPath));
				if (!MidPreparser.GetAvailableTracks(File.ReadAllBytes(file.UpdateMidiPath), out ulong update_tracks)) {
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= update_tracks;
			}
			// add upgrade midi, if it exists
			if(!string.IsNullOrEmpty(file.SongUpgrade.UpgradeMidiPath)){
				var upgrade_midi = file.SongUpgrade.GetUpgradeMidi();
				bytes.AddRange(upgrade_midi);
				if(!MidPreparser.GetAvailableTracks(upgrade_midi, out ulong upgrade_tracks)){
					return ScanResult.CorruptedNotesFile;
				}
				tracks |= upgrade_tracks;
			}

			string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes.ToArray())).Replace("-", "");

			file.CacheRoot = cache;
			file.Checksum = checksum;
			file.AvailableParts = tracks;

			return ScanResult.Ok;
		}
	}

}
