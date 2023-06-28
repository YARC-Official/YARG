using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;
using DtxCS.DataTypes;
using XboxSTFS;
using YARG.Audio;
using YARG.Serialization;
using YARG.Song.Preparsers;
using DtxCS;

namespace YARG.Song
{
    public class SongScanThread
    {
        private static readonly string SongsFilePath = Path.Combine("songs", "songs.dta");
        private static readonly string SongUpgradesFilePath = Path.Combine("song_upgrades", "upgrades.dta");

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

        private readonly Dictionary<CacheFolder, List<SongEntry>> _songsByCacheFolder;

        private readonly List<CacheFolder> _cacheErrors;
        private readonly Dictionary<string, List<SongError>> _songErrors;

        public IReadOnlyList<SongEntry> Songs => _songsByCacheFolder.Values.SelectMany(x => x).ToList();
        public IReadOnlyDictionary<string, List<SongError>> SongErrors => _songErrors;
        public IReadOnlyList<CacheFolder> CacheErrors => _cacheErrors;

        public SongScanThread(bool fast)
        {
            _songsByCacheFolder = new Dictionary<CacheFolder, List<SongEntry>>();
            _songErrors = new Dictionary<string, List<SongError>>();
            _cacheErrors = new List<CacheFolder>();
            _conFiles = new List<XboxSTFSFile>();

            _thread = fast ? new Thread(FastScan) : new Thread(FullScan);
        }

        public void AddFolder(CacheFolder cacheFolder)
        {
            if (IsScanning())
            {
                throw new Exception("Cannot add folder while scanning");
            }

            if (_songsByCacheFolder.ContainsKey(cacheFolder))
            {
                Debug.LogWarning("Two song folders with same directory!");
                return;
            }

            _songsByCacheFolder.Add(cacheFolder, new List<SongEntry>());
            _songErrors.Add(cacheFolder.Folder, new List<SongError>());
        }

        public bool IsScanning()
        {
            return _thread is not null && _thread.IsAlive;
        }

        public void Abort()
        {
            if (_thread is null || !_thread.IsAlive)
            {
                return;
            }

            _thread.Abort();
            _thread = null;
        }

        public bool StartScan()
        {
            if (_thread.IsAlive)
            {
                Debug.LogError("This scan thread is already in progress!");
                return false;
            }

            if (_songsByCacheFolder.Keys.Count == 0)
            {
                Debug.LogWarning("No song folders added to this thread");
                return false;
            }

            _thread.Start();
            return true;
        }

        private void FullScan()
        {
            Debug.Log("Performing full scan");
            foreach (var cache in _songsByCacheFolder.Keys)
            {
                // Folder doesn't exist, so report as an error and skip
                if (!Directory.Exists(cache.Folder))
                {
                    _songErrors[cache.Folder].Add(new SongError(cache.Folder, ScanResult.InvalidDirectory, ""));

                    Debug.LogError($"Invalid song directory: {cache}");
                    continue;
                }

                Debug.Log($"Scanning folder: {cache}");
                ScanSubDirectory(cache.Folder, cache.Folder, _songsByCacheFolder[cache]);

                Debug.Log($"Finished scanning {cache}, writing cache");

                cache.WriteCache(_songsByCacheFolder[cache], _conFiles);
                Debug.Log("Wrote cache");
            }
        }

        private void FastScan()
        {
            Debug.Log("Performing fast scan");

            // This is stupid
            var caches = new Dictionary<CacheFolder, List<SongEntry>>();
            foreach (var cache in _songsByCacheFolder.Keys)
            {
                try
                {
                    Debug.Log($"Reading cache of {cache}");
                    var cacheScan = cache.ReadCache();
                    caches.Add(cache, cacheScan.Item1);
                    _conFiles.AddRange(cacheScan.Item2);
                    Debug.Log($"Read cache of {cache}");
                }
                catch (Exception e)
                {
                    _cacheErrors.Add(cache);

                    Debug.LogException(e);
                    Debug.LogError($"Failed to read cache of {cache}");
                }
            }

            foreach (var (cache, songs) in caches)
            {
                _songsByCacheFolder[cache] = songs;
                Debug.Log($"Songs read from {cache}: {songs.Count}");
            }
        }

        private void ScanSubDirectory(string cacheFolder, string subDir, ICollection<SongEntry> songs)
        {
            _foldersScanned++;
            foldersScanned = _foldersScanned;

            if (_updateFolderPath.Length == 0)
            {
                string updatePath = Path.Combine(subDir, "songs_updates");
                if (Directory.Exists(updatePath))
                {
                    _updateFolderPath = updatePath;
                    Debug.Log($"Song updates found at {_updateFolderPath}");
                    _songUpdateDict = XboxSongUpdateBrowser.FetchSongUpdates(_updateFolderPath);
                    Debug.Log($"Total count of song updates found: {_songUpdateDict.Count}");
                }
            }

            if (_upgradeFolderPath.Length == 0)
            {
                string upgradePath = Path.Combine(subDir, "songs_upgrades");
                if (Directory.Exists(upgradePath))
                {
                    _upgradeFolderPath = upgradePath;
                    Debug.Log($"Song upgrades found at {_upgradeFolderPath}");
                    _songUpgradeDict = XboxSongUpgradeBrowser.FetchSongUpgrades(_upgradeFolderPath, ref _conFiles);
                    Debug.Log($"Total count of song upgrades found: {_songUpgradeDict.Count}");
                }
            }

            // Check if it is a song folder
            var result = ScanIniSong(cacheFolder, subDir, out var song);
            switch (result)
            {
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
            string songsFolder = Path.Combine(subDir, "songs");
            var dtaTree = BrowseFolderForExCon(songsFolder, Path.Combine(subDir, "songs_upgrades"));
            if (dtaTree != null)
            {
                for (int i = 0; i < dtaTree.Count; i++)
                {
                    try
                    {
                        var currentArray = (DataArray) dtaTree[i];
                        ExtractedConSongEntry currentSong = new(songsFolder, currentArray);
                        var updateValue = UpdateAndUpgradeCon(currentSong);
                        if (ValidateConEntry(cacheFolder, subDir, currentSong, currentArray, updateValue))
                            songs.Add(currentSong);
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"Failed to load song, skipping...");
                        Debug.LogException(e);
                    }
                }

                return;
            }

            // Iterate through the files in this current directory to look for CON files
            try
            {
                // try-catch to prevent crash if user doesn't have permission to access a folder
                foreach (var file in Directory.EnumerateFiles(subDir))
                {
                    XboxSTFSFile conFile = XboxSTFSFile.LoadCON(file);
                    if (conFile == null) continue;

                    dtaTree = BrowseCON(conFile);
                    if (dtaTree == null) continue;

                    bool addConFile = false;
                    for (int i = 0; i < dtaTree.Count; i++)
                    {
                        try
                        {
                            var currentArray = (DataArray) dtaTree[i];
                            ConSongEntry currentSong = new(conFile, currentArray);
                            var updateValue = UpdateAndUpgradeCon(currentSong);
                            if (ValidateConEntry(cacheFolder, subDir, currentSong, currentArray, updateValue))
                            {
                                songs.Add(currentSong);
                                addConFile = true;
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.Log($"Failed to load song, skipping...");
                            Debug.LogException(e);
                        }
                    }

                    if (addConFile) _conFiles.Add(conFile);
                }

                string[] subdirectories = Directory.GetDirectories(subDir);
                foreach (string subdirectory in subdirectories)
                {
                    if (subdirectory != _updateFolderPath && subdirectory != _upgradeFolderPath)
                    {
                        ScanSubDirectory(cacheFolder, subdirectory, songs);
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private DataArray BrowseFolderForExCon(string songsFolder, string songsUpgradesFolder)
        {
            string dtaPath = Path.Combine(songsUpgradesFolder, "upgrades.dta");
            if (File.Exists(dtaPath))
            {
                var dtaTree = DTX.FromPlainTextBytes(File.ReadAllBytes(dtaPath));

                for (int i = 0; i < dtaTree.Count; i++)
                {
                    try
                    {
                        _songUpgradeDict.Add(new(new SongProUpgrade(songsUpgradesFolder, dtaTree[i].Name),
                            (DataArray) dtaTree[i]));
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"Failed to get upgrade, skipping...");
                        Debug.LogException(e);
                    }
                }
            }

            dtaPath = Path.Combine(songsFolder, "songs.dta");
            if (File.Exists(dtaPath))
            {
                try
                {
                    return DTX.FromPlainTextBytes(File.ReadAllBytes(dtaPath));
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse songs.dta for `{songsFolder}`.");
                    Debug.LogException(e);
                }
            }

            return null;
        }

        private DataArray BrowseCON(XboxSTFSFile conFile)
        {
            byte[] dtaFile = conFile.LoadSubFile(SongUpgradesFilePath);
            if (dtaFile.Length > 0)
            {
                var dtaTree = DTX.FromPlainTextBytes(dtaFile);

                // Read each shortname the dta file lists
                for (int i = 0; i < dtaTree.Count; i++)
                {
                    try
                    {
                        _songUpgradeDict.Add(new(new SongProUpgrade(conFile, dtaTree[i].Name), (DataArray) dtaTree[i]));
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"Failed to get upgrade, skipping...");
                        Debug.LogException(e);
                    }
                }
            }

            dtaFile = conFile.LoadSubFile(SongsFilePath);
            if (dtaFile.Length != 0)
            {
                try
                {
                    return DTX.FromPlainTextBytes(dtaFile);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Failed to parse songs.dta for `{conFile.Filename}`.");
                    Debug.LogException(e);
                }
            }
            else
                Debug.Log("DTA file was not located in CON");

            return null;
        }

        private List<DataArray> UpdateAndUpgradeCon(ExtractedConSongEntry currentSong)
        {
            if (_songUpdateDict.TryGetValue(currentSong.ShortName, out var updateValue))
            {
                foreach (var dtaUpdate in updateValue) currentSong.SetFromDTA(dtaUpdate);
                currentSong.Update(_updateFolderPath);
            }

            currentSong.Upgrade(_songUpgradeDict);
            return updateValue;
        }

        private bool ValidateConEntry(string cacheFolder, string subDir, ExtractedConSongEntry currentSong,
            DataArray currentArray, List<DataArray> updateValue)
        {
            var exConResult = ScanConSong(cacheFolder, currentSong);
            if (exConResult == ScanResult.Ok)
            {
                _songsScanned++;
                songsScanned = _songsScanned;
                MoggBASSInfoGenerator.Generate(currentSong, currentArray.Array("song"), updateValue);
                return true;
            }

            if (exConResult != ScanResult.NotASong)
            {
                _errorsEncountered++;
                errorsEncountered = _errorsEncountered;
                _songErrors[cacheFolder].Add(new SongError(subDir, exConResult, currentSong.Name));
                Debug.LogWarning($"Error encountered with {subDir}");
            }

            return false;
        }

        private static ScanResult ScanIniSong(string cache, string directory, out IniSongEntry song)
        {
            // Usually we could infer metadata from a .chart file if no ini exists
            // But for now we just skip this song.
            song = null;
            if (!File.Exists(Path.Combine(directory, "song.ini")))
            {
                return ScanResult.NotASong;
            }

            if (!File.Exists(Path.Combine(directory, "notes.chart")) &&
                !File.Exists(Path.Combine(directory, "notes.mid")))
            {
                return ScanResult.NoNotesFile;
            }

            if (AudioHelpers.GetSupportedStems(directory).Count == 0)
            {
                return ScanResult.NoAudioFile;
            }

            string notesFile = File.Exists(Path.Combine(directory, "notes.chart")) ? "notes.chart" : "notes.mid";

            // Windows has a 260 character limit for file paths, so we need to check this
            if (Path.Combine(directory, notesFile).Length >= 255)
            {
                return ScanResult.NoNotesFile;
            }

            byte[] bytes = File.ReadAllBytes(Path.Combine(directory, notesFile));

            string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes)).Replace("-", "");

            var tracks = ulong.MaxValue;

            if (notesFile.EndsWith(".mid"))
            {
                if (!MidPreparser.GetAvailableTracks(bytes, out ulong availTracks))
                {
                    return ScanResult.CorruptedNotesFile;
                }

                tracks = availTracks;
            }
            else if (notesFile.EndsWith(".chart"))
            {
                if (!ChartPreparser.GetAvailableTracks(bytes, out ulong availTracks))
                {
                    return ScanResult.CorruptedNotesFile;
                }

                tracks = availTracks;
            }

            song = new IniSongEntry(cache, directory, checksum, notesFile, tracks);
            return song.ParseIni();
        }

        private static ScanResult ScanConSong(string cache, ExtractedConSongEntry file)
        {
            // Skip if the song doesn't have notes
            if (!file.ValidateMidiFile())
            {
                return ScanResult.NoNotesFile;
            }

            // Skip if this is a "fake song" (tutorials, etc.)
            if (file.IsFake)
            {
                return ScanResult.NotASong;
            }

            // Check if audio files
            if (file is ConSongEntry con)
            {
                if (con.MoggIndex == -1 && string.IsNullOrEmpty(con.MoggPath))
                {
                    return ScanResult.NoAudioFile;
                }
            }
            else if (string.IsNullOrEmpty(file.MoggPath))
            {
                return ScanResult.NoAudioFile;
            }

            if (!file.IsMoggUnencrypted())
            {
                return ScanResult.EncryptedMogg;
            }

            // all good - go ahead and build the cache info
            var bytes = new List<byte>();

            // add base midi
            byte[] notes = file.LoadMidiFile();
            bytes.AddRange(notes);
            if (!MidPreparser.GetAvailableTracks(notes, out ulong baseTracks))
            {
                return ScanResult.CorruptedNotesFile;
            }

            // add update midi, if it exists
            ulong tracks = baseTracks;
            if (file.DiscUpdate)
            {
                byte[] update = file.LoadMidiUpdateFile();
                bytes.AddRange(update);
                if (!MidPreparser.GetAvailableTracks(update, out ulong updateTracks))
                {
                    return ScanResult.CorruptedNotesFile;
                }

                tracks |= updateTracks;
            }

            // add upgrade midi, if it exists
            if (file.SongUpgrade != null)
            {
                byte[] upgradeMidi = file.SongUpgrade.GetUpgradeMidi();
                bytes.AddRange(upgradeMidi);
                if (!MidPreparser.GetAvailableTracks(upgradeMidi, out ulong upgradeTracks))
                {
                    return ScanResult.CorruptedNotesFile;
                }

                tracks |= upgradeTracks;
            }

            string checksum = BitConverter.ToString(SHA1.Create().ComputeHash(bytes.ToArray())).Replace("-", "");
            file.FinishScan(cache, checksum, tracks);
            return ScanResult.Ok;
        }
    }
}