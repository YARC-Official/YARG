using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Audio;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.Logging;

namespace YARG.Core.Song.Cache
{
    public enum ScanStage
    {
        LoadingCache,
        LoadingSongs,
        CleaningDuplicates,
        Sorting,
        WritingCache,
        WritingBadSongs
    }

    public struct ScanProgressTracker
    {
        public ScanStage Stage;
        public int Count;
        public int NumScannedDirectories;
        public int BadSongCount;
    }

    public abstract class CacheHandler
    {
        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        public const int CACHE_VERSION = 24_10_29_01;

        public static ScanProgressTracker Progress => _progress;
        private static ScanProgressTracker _progress;

        public static SongCache RunScan(bool tryQuickScan, string cacheLocation, string badSongsLocation, bool multithreading, bool allowDuplicates, bool fullDirectoryPlaylists, List<string> baseDirectories)
        {
            CacheHandler handler = multithreading
                ? new ParallelCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists)
                : new SequentialCacheHandler(baseDirectories, allowDuplicates, fullDirectoryPlaylists);

            // Some ini entry items won't come with the song length defined in the .ini file.
            // In those instances, we'll need to attempt to load the audio files that accompany the chart
            // to evaluate the length directly.
            // This toggle simply keeps those generated mixers from spamming the logs on creation.
            GlobalAudioHandler.LogMixerStatus = false;
            try
            {
                // Quick scans only fail if they parse zero entries (which could be the result of a few things)
                if (!tryQuickScan || !QuickScan(handler, cacheLocation))
                {
                    // If a quick scan failed, there's no point to re-reading it in the full scan
                    FullScan(handler, !tryQuickScan, cacheLocation, badSongsLocation);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Unknown error while running song scan!");
            }
            GlobalAudioHandler.LogMixerStatus = true;
            return handler.cache;
        }

        /// <summary>
        /// Reads the entries from a cache file - performing very few validation checks on the entries contained within
        /// for the sole purpose of speeding through to gameplay.
        /// </summary>
        /// <param name="handler">A parallel or sequential handler</param>
        /// <param name="cacheLocation">File path of the cache</param>
        /// <returns>Whether the scan sucessfully parsed entries</returns>
        private static bool QuickScan(CacheHandler handler, string cacheLocation)
        {
            try
            {
                using var cacheFile = LoadCacheToMemory(cacheLocation, handler.fullDirectoryPlaylists);
                if (cacheFile.IsAllocated)
                {
                    _progress.Stage = ScanStage.LoadingCache;
                    YargLogger.LogDebug("Quick Read start");
                    handler.Deserialize_Quick(cacheFile.ToStream());
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error occurred during quick cache file read!");
            }

            if (_progress.Count == 0)
            {
                return false;
            }

            handler.CleanupDuplicates();

            _progress.Stage = ScanStage.Sorting;
            handler.SortEntries();
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);
            return true;
        }

        /// <summary>
        /// Runs a full scan process for a user's library.
        /// Firstly, it attempts to read entries from a cache file - performing all validation checks necessary
        /// to ensure that the player can immediately play whatever comes off the cache.
        /// Secondly, we traverse the user's filesystem starting from their provided base directory nodes for any entries
        /// that were not found from the cache or required re-evaluating.
        /// Finally, we write the results of the scan back to a cache file and, if necessary, a badsongs.txt file containing the failures.
        /// </summary>
        /// <param name="handler">A parallel or sequential handler</param>
        /// <param name="loadCache">A flag communicating whether to perform the cache read (false only from failed quick scans)</param>
        /// <param name="cacheLocation">File path of the cache</param>
        /// <param name="badSongsLocation">File path of the badsongs.txt</param>
        private static void FullScan(CacheHandler handler, bool loadCache, string cacheLocation, string badSongsLocation)
        {
            if (loadCache)
            {
                try
                {
                    using var cacheFile = LoadCacheToMemory(cacheLocation, handler.fullDirectoryPlaylists);
                    if (cacheFile.IsAllocated)
                    {
                        _progress.Stage = ScanStage.LoadingCache;
                        YargLogger.LogDebug("Full Read start");
                        handler.Deserialize(cacheFile.ToStream());
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex, "Error occurred during full cache file read!");
                }
            }

            _progress.Stage = ScanStage.LoadingSongs;
            handler.FindNewEntries();
            // CON, Upgrade, and Update groups hold onto the DTA data in memory.
            // Once all entries are processed, they are no longer useful to us, so we dispose of them here.
            handler.DisposeLeftoverData();

            _progress.Stage = ScanStage.CleaningDuplicates;
            handler.CleanupDuplicates();

            _progress.Stage = ScanStage.Sorting;
            handler.SortEntries();
            YargLogger.LogFormatDebug("Total Entries: {0}", _progress.Count);

            try
            {
                _progress.Stage = ScanStage.WritingCache;
                handler.Serialize(cacheLocation);
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error when writing song cache!");
            }

            try
            {
                if (handler.badSongs.Count > 0)
                {
                    _progress.Stage = ScanStage.WritingBadSongs;
                    handler.WriteBadSongs(badSongsLocation);
                }
                else
                {
                    File.Delete(badSongsLocation);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, "Error when writing bad songs file!");
            }
        }

        #region Data

        protected readonly SongCache cache = new();

        protected readonly List<IniGroup> iniGroups;
        protected readonly List<UpdateGroup> updateGroups = new();
        protected readonly List<UpgradeGroup> upgradeGroups = new();
        protected readonly List<PackedCONGroup> conGroups = new();
        protected readonly List<UnpackedCONGroup> extractedConGroups = new();
        protected readonly Dictionary<string, CONModification> conModifications = new();
        protected readonly Dictionary<string, RBProUpgrade> cacheUpgrades = new();
        protected readonly HashSet<string> preScannedDirectories = new();
        protected readonly HashSet<string> preScannedFiles = new();


        protected readonly bool allowDuplicates = true;
        protected readonly bool fullDirectoryPlaylists;
        protected readonly List<SongEntry> duplicatesRejected = new();
        protected readonly List<SongEntry> duplicatesToRemove = new();
        protected readonly SortedDictionary<string, ScanResult> badSongs = new();
        #endregion

        #region Common

        protected CacheHandler(List<string> baseDirectories, bool allowDuplicates, bool fullDirectoryPlaylists)
        {
            _progress = default;
            this.allowDuplicates = allowDuplicates;
            this.fullDirectoryPlaylists = fullDirectoryPlaylists;

            iniGroups = new(baseDirectories.Count);
            foreach (string dir in baseDirectories)
            {
                if (!string.IsNullOrEmpty(dir) && !iniGroups.Exists(group => { return group.Directory == dir; }))
                {
                    iniGroups.Add(new IniGroup(dir));
                }
            }
        }

        /// <summary>
        /// Sorts entries
        /// </summary>
        protected abstract void SortEntries();

        /// <summary>
        /// Adds a instance to the shared list of packed con groups.
        /// </summary>
        protected abstract void AddPackedCONGroup(PackedCONGroup group);

        /// <summary>
        /// Adds a instance to the shared list of unpacked con groups.
        /// </summary>
        protected abstract void AddUnpackedCONGroup(UnpackedCONGroup group);

        /// <summary>
        /// Adds a instance to the shared list of update groups.
        /// </summary>
        protected abstract void AddUpdateGroup(UpdateGroup group);

        /// <summary>
        /// Adds a instance to the shared list of upgrade groups.
        /// </summary>
        protected abstract void AddUpgradeGroup(UpgradeGroup group);

        /// <summary>
        /// Removes all the entries present in all packed and unpacked con groups that have a matching DTA node name
        /// </summary>
        protected abstract void RemoveCONEntry(string shortname);

        /// <summary>
        /// Grabs or constructs a node containing all the updates or upgrades that can applied to any DTA entries
        /// that have a name matching the one provided.
        /// </summary>
        /// <param name="name">The name of the DTA node for the entry</param>
        /// <returns>The node with the update and upgrade information</returns>
        protected abstract CONModification GetModification(string name);

        /// <summary>
        /// Performs the traversal of the filesystem in search of new entries to add to a user's library
        /// </summary>
        protected abstract void FindNewEntries();

        /// <summary>
        /// Splits a collection into the tasks that traverse subdirectories or scan subfiles during the "New Entries" process
        /// </summary>
        /// <param name="collection">The collection containing the sub directories and/or files</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="tracker">A tracker used to apply provide entries with default playlists</param>
        protected abstract void TraverseDirectory(in FileCollection collection, IniGroup group, PlaylistTracker tracker);

        /// <summary>
        /// Deserializes a cache file into the separate song entries with all validation checks
        /// </summary>
        /// <param name="stream">The stream containging the cache file data</param>
        protected abstract void Deserialize(UnmanagedMemoryStream stream);

        /// <summary>
        /// Deserializes a cache file into the separate song entries with minimal validations
        /// </summary>
        /// <param name="stream">The stream containging the cache file data</param>
        protected abstract void Deserialize_Quick(UnmanagedMemoryStream stream);

        /// <summary>
        /// Adds a collection constructed during the full deserialization to a cache for use during
        /// the full scan "New Entries" step. This skips the need to re-process a directory's list of files
        /// where applicable.
        /// </summary>
        protected abstract void AddCollectionToCache(in FileCollection collection);

        /// <summary>
        /// Returns a CON group if the upgradeCON deserialization step already generated a group with the same filename 
        /// </summary>
        /// <param name="filename">The file path of the CON to potentially load (if not already)</param>
        /// <returns>A pre-loaded CON group on success find; <see langword="null"/> otherwise</returns>
        protected abstract PackedCONGroup? FindCONGroup(string filename);

        /// <summary>
        /// Upgrade nodes need to exist before CON entries can be processed. We therefore place all valid upgrades from cache
        /// into a list for quick access.
        /// </summary>
        /// <param name="name">The DTA node name for the upgrade</param>
        /// <param name="upgrade">The upgrade node to add</param>
        protected abstract void AddCacheUpgrade(string name, RBProUpgrade upgrade);

        /// <summary>
        /// Attempts to mark a directory as "processed"
        /// </summary>
        /// <param name="directory">The directory to mark</param>
        /// <returns><see langword="true"/> if the directory was not previously marked</returns>
        protected virtual bool FindOrMarkDirectory(string directory)
        {
            if (!preScannedDirectories.Add(directory))
            {
                return false;
            }
            _progress.NumScannedDirectories++;
            return true;
        }

        /// <summary>
        /// Attempts to mark a file as "processed"
        /// </summary>
        /// <param name="file">The file to mark</param>
        /// <returns><see langword="true"/> if the file was not previously marked</returns>
        protected virtual bool FindOrMarkFile(string file)
        {
            return preScannedFiles.Add(file);
        }

        /// <summary>
        /// Adds an instance of a bad song
        /// </summary>
        /// <param name="filePath">The file that produced the error</param>
        /// <param name="err">The error produced</param>
        protected virtual void AddToBadSongs(string filePath, ScanResult err)
        {
            badSongs.Add(filePath, err);
            _progress.BadSongCount++;
        }

        /// <summary>
        /// Attempts to add a new entry to current list. If duplicates are allowed, this will always return true.
        /// If they are disallowed, then this will only succeed if the entry is not a duplicate or if it
        /// takes precedence over the entry currently in its place (based on a variety of factors)
        /// </summary>
        /// <param name="entry">The entry to add</param>
        /// <returns>Whether the song was accepted into the list</returns>
        protected virtual bool AddEntry(SongEntry entry)
        {
            var hash = entry.Hash;
            if (cache.Entries.TryGetValue(hash, out var list) && !allowDuplicates)
            {
                if (list[0].IsPreferedOver(entry))
                {
                    duplicatesRejected.Add(entry);
                    return false;
                }

                duplicatesToRemove.Add(list[0]);
                list[0] = entry;
            }
            else
            {
                if (list == null)
                {
                    cache.Entries.Add(hash, list = new List<SongEntry>());
                }

                list.Add(entry);
                ++_progress.Count;
            }
            return true;
        }

        /// <summary>
        /// Marks a CON song with the DTA name as invalid for addition from the cache
        /// </summary>
        /// <param name="name">The DTA name to mark</param>
        protected virtual void AddInvalidSong(string name)
        {
            invalidSongsInCache.Add(name);
        }

        /// <summary>
        /// Grabs the iniGroup that parents the provided path, if one exists
        /// </summary>
        /// <param name="path">The absolute file path</param>
        /// <returns>The applicable group if found; <see langword="null"/> otherwise</returns>
        protected IniGroup? GetBaseIniGroup(string path)
        {
            foreach (var group in iniGroups)
            {
                if (path.StartsWith(group.Directory) &&
                    // Ensures directories with similar names (previously separate bases)
                    // that are consolidated in-game to a single base directory
                    // don't have conflicting "relative path" issues
                    (path.Length == group.Directory.Length || path[group.Directory.Length] == Path.DirectorySeparatorChar))
                    return group;
            }
            return null;
        }

        /// <summary>
        /// Disposes all DTA FixedArray data present in upgrade and update nodes.
        /// The songDTA arrays will already have been disposed of before reaching this point.
        /// </summary>
        private void DisposeLeftoverData()
        {
            foreach (var group in conGroups)
            {
                group.UpgradeDTAData.Dispose();
            }

            foreach (var group in upgradeGroups)
            {
                group.DTAData.Dispose();
            }

            foreach (var group in updateGroups)
            {
                group.DTAData.Dispose();
            }
        }

        /// <summary>
        /// Goes through all the groups that contain song entries to remove specific instances of duplicates
        /// </summary>
        private void CleanupDuplicates()
        {
            foreach (var entry in duplicatesToRemove)
            {
                if (!TryRemove(iniGroups, entry) && !TryRemove(conGroups, entry))
                {
                    TryRemove(extractedConGroups, entry);
                }
            }
        }

        private static bool TryRemove<TGroup>(List<TGroup> groups, SongEntry entry)
            where TGroup : ICacheGroup
        {
            for (int i = 0; i < groups.Count; ++i)
            {
                var group = groups[i];
                if (group.TryRemoveEntry(entry))
                {
                    if (group.Count == 0)
                    {
                        groups.RemoveAt(i);
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Writes all bad song instances to a badsongs.txt file for the user
        /// </summary>
        /// <param name="badSongsLocation">The path for the file</param>
        private void WriteBadSongs(string badSongsLocation)
        {
            using var stream = new FileStream(badSongsLocation, FileMode.Create, FileAccess.Write);
            using var writer = new StreamWriter(stream);

            writer.WriteLine($"Total Errors: {badSongs.Count}");
            writer.WriteLine();

            foreach (var error in badSongs)
            {
                writer.WriteLine(error.Key);
                switch (error.Value)
                {
                    case ScanResult.DirectoryError:
                        writer.WriteLine("Error accessing directory contents");
                        break;
                    case ScanResult.DuplicateFilesFound:
                        writer.WriteLine("Multiple sub files or directories that share the same name found in this location.");
                        writer.WriteLine("You must rename or remove all duplicates before they will be processed.");
                        break;
                    case ScanResult.IniEntryCorruption:
                        writer.WriteLine("Corruption of either the ini file or chart/mid file");
                        break;
                    case ScanResult.NoAudio:
                        writer.WriteLine("No audio accompanying the chart file");
                        break;
                    case ScanResult.NoName:
                        writer.WriteLine("Name metadata not provided");
                        break;
                    case ScanResult.NoNotes:
                        writer.WriteLine("No notes found");
                        break;
                    case ScanResult.DTAError:
                        writer.WriteLine("Error occured while parsing DTA file node");
                        break;
                    case ScanResult.MoggError:
                        writer.WriteLine("Required mogg audio file not present or used invalid encryption");
                        break;
                    case ScanResult.UnsupportedEncryption:
                        writer.WriteLine("Mogg file uses unsupported encryption");
                        break;
                    case ScanResult.MissingCONMidi:
                        writer.WriteLine("Midi file queried for found missing");
                        break;
                    case ScanResult.IniNotDownloaded:
                        writer.WriteLine("Ini file not fully downloaded - try again once it completes");
                        break;
                    case ScanResult.ChartNotDownloaded:
                        writer.WriteLine("Chart file not fully downloaded - try again once it completes");
                        break;
                    case ScanResult.PossibleCorruption:
                        writer.WriteLine("Possible corruption of a queried midi file");
                        break;
                    case ScanResult.FailedSngLoad:
                        writer.WriteLine("File structure invalid or corrupted");
                        break;
                    case ScanResult.PathTooLong:
                        writer.WriteLine("Path too long for the Windows Filesystem (path limitation can be changed in registry settings if you so wish)");
                        break;
                    case ScanResult.MultipleMidiTrackNames:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous)");
                        break;
                    case ScanResult.MultipleMidiTrackNames_Update:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous) - Thrown by a midi update");
                        break;
                    case ScanResult.MultipleMidiTrackNames_Upgrade:
                        writer.WriteLine("At least one track fails midi spec for containing multiple unique track names (thus making it ambiguous) - Thrown by a pro guitar upgrade");
                        break;
                    case ScanResult.LooseChart_Warning:
                        writer.WriteLine("Loose chart files halted all traversal into the subdirectories at this location.");
                        writer.WriteLine("To fix, if desired, place the loose chart files in a separate dedicated folder.");
                        break;
                    case ScanResult.InvalidResolution:
                        writer.WriteLine("This chart uses an invalid resolution (or possibly contains it in an improper format, if .chart)");
                        break;
                    case ScanResult.InvalidResolution_Update:
                        writer.WriteLine("The midi chart update file applicable with this chart has an invalid resolution of zero");
                        break;
                    case ScanResult.InvalidResolution_Upgrade:
                        writer.WriteLine("The midi pro guitar upgrade file applicable with this chart has an invalid resolution of zero");
                        break;
                }
                writer.WriteLine();
            }
        }
        #endregion

        #region Scanning

        protected readonly struct PlaylistTracker
        {
            private readonly bool _fullDirectoryFlag;
            // We use `null` as the default state to grant two levels of subdirectories before
            // supplying directories as the actual playlist (null -> empty -> directory)
            private readonly string? _playlist;

            public string Playlist => !string.IsNullOrEmpty(_playlist) ? _playlist : "Unknown Playlist";

            public PlaylistTracker(bool fullDirectoryFlag, string? playlist)
            {
                _fullDirectoryFlag = fullDirectoryFlag;
                _playlist = playlist;
            }

            public PlaylistTracker Append(string directory)
            {
                string playlist = string.Empty;
                if (_playlist != null)
                {
                    playlist = _fullDirectoryFlag ? Path.Combine(_playlist, directory) : directory;
                }
                return new PlaylistTracker(_fullDirectoryFlag, playlist);
            }
        }

        protected const string SONGS_DTA = "songs.dta";
        protected const string SONGUPDATES_DTA = "songs_updates.dta";
        protected const string SONGUPGRADES_DTA = "upgrades.dta";

        /// <summary>
        /// Checks for the presence of files pertaining to an unpacked ini entry or whether the directory
        /// is to be used for CON updates, upgrades, or extracted CON song entries.
        /// If none of those, this will further traverse through any of the subdirectories present in this directory
        /// and process all the subfiles for potential CONs or SNGs.
        /// </summary>
        /// <param name="directory">The directory instance to load and scan through</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="tracker">A tracker used to apply provide entries with default playlists</param>
        protected void ScanDirectory(DirectoryInfo directory, IniGroup group, PlaylistTracker tracker)
        {
            try
            {
                if (!FindOrMarkDirectory(directory.FullName) || (directory.Attributes & FileAttributes.Hidden) != 0)
                {
                    return;
                }

                // An update, upgrade, or unpacked con group might've failed during the cache load.
                // In certain conditions, the collections that we would otherwise use for those would instead be in this cache
                if (!collectionCache.TryGetValue(directory.FullName, out var collection))
                {
                    collection = new FileCollection(directory);
                }

                // If we discover any combo of valid unpacked ini entry files in this directory,
                // we will traverse none of the subdirectories present in this scope
                if (ScanIniEntry(in collection, group, tracker.Playlist))
                {
                    // However, the presence subdirectories could mean that the user didn't properly
                    // organize their collection. So as a service, we warn them in the badsongs.txt.
                    if (collection.SubDirectories.Count > 0)
                    {
                        AddToBadSongs(directory.FullName, ScanResult.LooseChart_Warning);
                    }
                    return;
                }

                switch (directory.Name)
                {
                    // FOR ALL OF THE CASES: a missing dta file means that we will treat the folder like any other subdirectory
                    case "songs_updates":
                        {
                            if (collection.Subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
                            {
                                var updateGroup = CreateUpdateGroup(in collection, dta);
                                if (updateGroup != null)
                                {
                                    // Ensures any con entries pulled from cache are removed for re-evaluation
                                    foreach (var node in updateGroup.Updates)
                                    {
                                        RemoveCONEntry(node.Key);
                                    }
                                }
                                return;
                            }
                            break;
                        }
                    case "songs_upgrades":
                        {
                            if (collection.Subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
                            {
                                var upgradeGroup = CreateUpgradeGroup(in collection, dta);
                                if (upgradeGroup != null)
                                {
                                    // Ensures any con entries pulled from cache are removed for re-evaluation
                                    foreach (var node in upgradeGroup.Upgrades)
                                    {
                                        RemoveCONEntry(node.Key);
                                    }
                                }
                                return;
                            }
                            break;
                        }
                    case "songs":
                        {
                            if (collection.Subfiles.TryGetValue(SONGS_DTA, out var dta))
                            {
                                var _ = CreateUnpackedCONGroup(directory.FullName, dta, tracker.Playlist);
                                return;
                            }
                            break;
                        }
                }

                TraverseDirectory(collection, group, tracker.Append(directory.Name));
                // Only possible on UNIX-based systems where file names are case-sensitive
                if (collection.ContainedDupes)
                {
                    AddToBadSongs(collection.Directory.FullName, ScanResult.DuplicateFilesFound);
                }
            }
            catch (PathTooLongException)
            {
                YargLogger.LogFormatError("Path {0} is too long for the file system!", directory.FullName);
                AddToBadSongs(directory.FullName, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning directory {directory.FullName}!");
            }
        }

        /// <summary>
        /// Attempts to process the provided file as either a CON or SNG
        /// </summary>
        /// <param name="info">The info for provided file</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="tracker">A tracker used to apply provide entries with default playlists</param>
        protected void ScanFile(FileInfo info, IniGroup group, in PlaylistTracker tracker)
        {
            string filename = info.FullName;
            try
            {
                // Ensures only fully downloaded unmarked files are processed
                if (FindOrMarkFile(filename) && (info.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) == 0)
                {
                    var abridged = new AbridgedFileInfo(info);
                    string ext = info.Extension;
                    if (ext == ".sng" || ext == ".yargsong")
                    {
                        using var sngFile = SngFile.TryLoadFromFile(abridged);
                        if (sngFile != null)
                        {
                            ScanSngFile(sngFile, group, tracker.Playlist);
                        }
                        else
                        {
                            AddToBadSongs(info.FullName, ScanResult.PossibleCorruption);
                        }
                    }
                    else
                    {
                        var conGroup = CreateCONGroup(in abridged, tracker.Playlist);
                        if (conGroup != null)
                        {
                            // Ensures any con entries pulled from cache are removed for re-evaluation
                            foreach (var node in conGroup.Upgrades)
                            {
                                RemoveCONEntry(node.Key);
                            }
                        }
                    }
                }
            }
            catch (PathTooLongException)
            {
                YargLogger.LogFormatError("Path {0} is too long for the file system!", filename);
                AddToBadSongs(filename, ScanResult.PathTooLong);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Error while scanning file {filename}!");
            }
        }

        /// <summary>
        /// A templated helper function used for scanning a new CON entry to the list
        /// </summary>
        /// <typeparam name="TGroup">The group type (shocker)</typeparam>
        /// <typeparam name="TEntry">The entry type for the group (again... shocker)</typeparam>
        /// <param name="group">The group that contains or will contain the entry</param>
        /// <param name="name">The DTA node name for the entry</param>
        /// <param name="index">The index for the specific node (for CON packs that contain songs that share the same DTA name FOR SOME FUCKING REASON)</param>
        /// <param name="node">The raw byte data for the entry's base DTA information</param>
        /// <param name="func">The function used to convert the DTA info and modifications to the desired entry type</param>
        protected unsafe void ScanCONNode<TGroup, TEntry>(TGroup group, string name, int index, in YARGTextContainer<byte> node, delegate*<TGroup, string, DTAEntry, CONModification, (ScanResult, TEntry?)> func)
            where TGroup : CONGroup<TEntry>
            where TEntry : RBCONEntry
        {
            if (group.TryGetEntry(name, index, out var entry))
            {
                if (!AddEntry(entry!))
                {
                    group.RemoveEntry(name, index);
                }
            }
            else
            {
                try
                {
                    var dtaEntry = new DTAEntry(name, in node);
                    var modification = GetModification(name);
                    var song = func(group, name, dtaEntry, modification);
                    if (song.Item2 != null)
                    {
                        if (AddEntry(song.Item2))
                        {
                            group.AddEntry(name, index, song.Item2);
                        }
                    }
                    else
                    {
                        AddToBadSongs(group.Location + $" - Node {name}", song.Item1);
                    }
                }
                catch (Exception ex)
                {
                    YargLogger.LogException(ex);
                    AddToBadSongs(group.Location + $" - Node {name}", ScanResult.DTAError);
                }
            }
        }

        /// <summary>
        /// Loads the updates and upgrades that apply to con entries that share the same DTA node name
        /// </summary>
        /// <param name="modification">The modification node to initialize</param>
        /// <param name="name">The DTA name of the node</param>
        protected void InitModification(CONModification modification, string name)
        {
            // To put the behavior simply: different folders mapping to the same nodes
            // for like modification types is no bueno. Only the one with the most recent DTA
            // write time will get utilized for entries with the current DTA name.

            var datetime = default(DateTime);
            foreach (var group in updateGroups)
            {
                if (group.Updates.TryGetValue(name, out var update))
                {
                    if (modification.UpdateDTA == null || datetime < group.DTALastWrite)
                    {
                        modification.UpdateDTA = new DTAEntry(update.Containers[0].Encoding);
                        foreach (var container in update.Containers)
                        {
                            modification.UpdateDTA.LoadData(name, container);
                        }
                        modification.Midi = update.Midi;
                        if (modification.Midi == null && modification.UpdateDTA.DiscUpdate)
                        {
                            YargLogger.LogFormatWarning("Update midi expected in directory {0}", Path.Combine(group.Directory, name));
                        }

                        modification.Mogg = update.Mogg;
                        modification.Milo = update.Milo;
                        modification.Image = update.Image;
                        datetime = group.DTALastWrite;
                    }
                }
            }

            foreach (var group in upgradeGroups)
            {
                if (group.Upgrades.TryGetValue(name, out var node) && node.Upgrade != null)
                {
                    if (modification.UpgradeDTA == null || datetime < group.DTALastWrite)
                    {
                        modification.UpgradeNode = node.Upgrade;
                        modification.UpgradeDTA = new DTAEntry(name, in node.Container);
                        datetime = group.DTALastWrite;
                    }
                }
            }

            foreach (var group in conGroups)
            {
                if (group.Upgrades.TryGetValue(name, out var node) && node.Upgrade != null)
                {
                    if (modification.UpgradeDTA == null || datetime < group.Info.LastUpdatedTime)
                    {
                        modification.UpgradeNode = node.Upgrade;
                        modification.UpgradeDTA = new DTAEntry(name, in node.Container);
                        datetime = group.Info.LastUpdatedTime;
                    }
                }
            }
        }

        /// <summary>
        /// Searches for a ".ini" and any .mid or .chart file to possibly extract as a song entry.
        /// If found, even if we can't extract an entry from them, we should perform no further directory traversal.
        /// </summary>
        /// <param name="collection">The collection containing the subfiles to search from</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="defaultPlaylist">The default directory-based playlist to use for any successful entry</param>
        /// <returns>Whether files pertaining to an unpacked ini entry were discovered</returns>
        private bool ScanIniEntry(in FileCollection collection, IniGroup group, string defaultPlaylist)
        {
            int i = collection.Subfiles.TryGetValue("song.ini", out var ini) ? 0 : 2;
            while (i < 3)
            {
                if (!collection.Subfiles.TryGetValue(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                // Can't play a song without any audio can you?
                //
                // Note though that this is purely a pre-add check.
                // We will not invalidate an entry from cache if the user removes the audio after the fact.
                if (!collection.ContainsAudio())
                {
                    AddToBadSongs(chart.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = UnpackedIniEntry.ProcessNewEntry(collection.Directory.FullName, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, ini, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(chart.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (PathTooLongException)
                {
                    YargLogger.LogFormatError("Path {0} is too long for the file system!", chart);
                    AddToBadSongs(chart.FullName, ScanResult.PathTooLong);
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while scanning chart file {chart}!");
                    AddToBadSongs(collection.Directory.FullName, ScanResult.IniEntryCorruption);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Searches for any .mid or .chart file to possibly extract as a song entry.
        /// </summary>
        /// <param name="sngFile">The sngfile to search through</param>
        /// <param name="group">The group aligning to one of the base directories provided by the user</param>
        /// <param name="defaultPlaylist">The default directory-based playlist to use for any successful entry</param>
        private void ScanSngFile(SngFile sngFile, IniGroup group, string defaultPlaylist)
        {
            int i = sngFile.Metadata.Count > 0 ? 0 : 2;
            while (i < 3)
            {
                if (!sngFile.TryGetValue(IniSubEntry.CHART_FILE_TYPES[i].Filename, out var chart))
                {
                    ++i;
                    continue;
                }

                if (!sngFile.ContainsAudio())
                {
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.NoAudio);
                    break;
                }

                try
                {
                    var entry = SngEntry.ProcessNewEntry(sngFile, chart, IniSubEntry.CHART_FILE_TYPES[i].Format, defaultPlaylist);
                    if (entry.Item2 == null)
                    {
                        AddToBadSongs(sngFile.Info.FullName, entry.Item1);
                    }
                    else if (AddEntry(entry.Item2))
                    {
                        group.AddEntry(entry.Item2);
                    }
                }
                catch (Exception e)
                {
                    YargLogger.LogException(e, $"Error while scanning chart file {chart} within {sngFile.Info.FullName}!");
                    AddToBadSongs(sngFile.Info.FullName, ScanResult.IniEntryCorruption);
                }
                break;
            }
        }
        #endregion

        #region Serialization

        public const int SIZEOF_DATETIME = 8;
        protected readonly HashSet<string> invalidSongsInCache = new();
        protected readonly Dictionary<string, FileCollection> collectionCache = new();

        /// <summary>
        /// The sum of all "count" variables in a file
        /// 4 - (version number(4 bytes))
        /// 1 - (FullDirectoryPlaylist flag(1 byte))
        /// 64 - (section size(4 bytes) + zero string count(4 bytes)) * # categories(8)
        /// 24 - (# groups(4 bytes) * # group types(6))
        ///
        /// </summary>
        private const int MIN_CACHEFILESIZE = 93;

        /// <summary>
        /// Attempts to laod the cache file's data into a FixedArray. This will fail if an error is thrown,
        /// the cache is outdated, or if the the "full playlist" toggle mismatches.
        /// </summary>
        /// <param name="cacheLocation">File location for the cache</param>
        /// <param name="fullDirectoryPlaylists">Toggle for the display style of directory-based playlists</param>
        /// <returns>A FixedArray instance pointing to a buffer of the cache file's data, or <see cref="FixedArray&lt;&rt;"/>.Null if invalid</returns>
        private static FixedArray<byte> LoadCacheToMemory(string cacheLocation, bool fullDirectoryPlaylists)
        {
            FileInfo info = new(cacheLocation);
            if (!info.Exists || info.Length < MIN_CACHEFILESIZE)
            {
                YargLogger.LogDebug("Cache invalid or not found");
                return FixedArray<byte>.Null;
            }

            using var stream = new FileStream(cacheLocation, FileMode.Open, FileAccess.Read);
            if (stream.Read<int>(Endianness.Little) != CACHE_VERSION)
            {
                YargLogger.LogDebug($"Cache outdated");
                return FixedArray<byte>.Null;
            }

            if (stream.ReadBoolean() != fullDirectoryPlaylists)
            {
                YargLogger.LogDebug($"FullDirectoryFlag flipped");
                return FixedArray<byte>.Null;
            }
            return FixedArray<byte>.ReadRemainder(stream);
        }

        /// <summary>
        /// Serializes the cache to a file, duhhhhhhh
        /// </summary>
        /// <param name="cacheLocation">Location to save to</param>
        private void Serialize(string cacheLocation)
        {
            using var filestream = new FileStream(cacheLocation, FileMode.Create, FileAccess.Write);
            Dictionary<SongEntry, CategoryCacheWriteNode> nodes = new();

            filestream.Write(CACHE_VERSION, Endianness.Little);
            filestream.Write(fullDirectoryPlaylists);

            CategoryWriter.WriteToCache(filestream, cache.Titles, SongAttribute.Name, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Artists, SongAttribute.Artist, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Albums, SongAttribute.Album, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Genres, SongAttribute.Genre, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Years, SongAttribute.Year, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Charters, SongAttribute.Charter, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Playlists, SongAttribute.Playlist, ref nodes);
            CategoryWriter.WriteToCache(filestream, cache.Sources, SongAttribute.Source, ref nodes);

            List<PackedCONGroup> upgradeCons = new();
            List<PackedCONGroup> entryCons = new();
            foreach (var group in conGroups)
            {
                if (group.Upgrades.Count > 0)
                    upgradeCons.Add(group);

                if (group.Count > 0)
                    entryCons.Add(group);
            }

            ICacheGroup.SerializeGroups(iniGroups, filestream, nodes);
            IModificationGroup.SerializeGroups(updateGroups, filestream);
            IModificationGroup.SerializeGroups(upgradeGroups, filestream);
            IModificationGroup.SerializeGroups(upgradeCons, filestream);
            ICacheGroup.SerializeGroups(entryCons, filestream, nodes);
            ICacheGroup.SerializeGroups(extractedConGroups, filestream, nodes);
        }

        /// <summary>
        /// Reads a ini-based entry from the cache, with all validation steps
        /// </summary>
        /// <param name="group">Group mapping to the *user's* base directory</param>
        /// <param name="directory">String of the base directory *written in the cache*</param>
        /// <param name="stream">Stream containing the entry data</param>
        /// <param name="strings">Container of the main metadata arrays</param>
        protected void ReadIniEntry(IniGroup group, string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            // An ini entry can be either unpacked (.ini) or packed (.sng).
            // This boolean variable in the cache communicates to us which type it is.
            bool isSngEntry = stream.ReadBoolean();
            string fullname = Path.Combine(directory, stream.ReadString());

            IniSubEntry? entry = isSngEntry
                ? ReadSngEntry(fullname, stream, strings)
                : ReadUnpackedIniEntry(fullname, stream, strings);

            // If the "duplicates" toggle is set to false, regardless of what's within the cache,
            // we will only accept non-duplicates
            if (entry != null && AddEntry(entry))
            {
                group.AddEntry(entry);
            }
        }

        /// <summary>
        /// Reads a ini-based entry from the cache, with very few validation steps
        /// </summary>
        /// <param name="directory">String of the base directory *written in the cache*</param>
        /// <param name="stream">Stream containing the entry data</param>
        /// <param name="strings">Container of the main metadata arrays</param>
        protected void QuickReadIniEntry(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            // An ini entry can be either unpacked (.ini) or packed (.sng).
            // This boolean variable in the cache communicates to us which type it is.
            bool isSngEntry = stream.ReadBoolean();
            string fullname = Path.Combine(directory, stream.ReadString());

            IniSubEntry? entry = isSngEntry
                ? SngEntry.LoadFromCache_Quick(fullname, stream, strings)
                : UnpackedIniEntry.IniFromCache_Quick(fullname, stream, strings);

            if (entry != null)
            {
                // If the "duplicates" toggle is set to false, regardless of what's within the cache,
                // we will only accept non-duplicates
                AddEntry(entry);
            }
            else
            {
                YargLogger.LogError("Cache file was modified externally with a bad CHART_TYPE enum value... or bigger error");
            }
        }

        /// <summary>
        /// Reads a section of the cache containing a list of updates to apply from a specific directory,
        /// performing validations on each update node. If an update node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of updates</param>
        protected void ReadUpdateDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) == null)
            {
                goto Invalidate;
            }

            var dirInfo = new DirectoryInfo(directory);
            if (!dirInfo.Exists)
            {
                goto Invalidate;
            }

            var collection = new FileCollection(dirInfo);
            if (!collection.Subfiles.TryGetValue(SONGUPDATES_DTA, out var dta))
            {
                // We don't *mark* the directory to allow the "New Entries" process
                // to access this collection
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            // Will add the update group to the shared list on success
            var group = CreateUpdateGroup(in collection, dta);
            if (group != null && group.DTALastWrite == dtaLastWritten)
            {
                // We need to compare what we have on the filesystem against what's written one by one
                var updates = new Dictionary<string, SongUpdate>(group.Updates);
                for (int i = 0; i < count; i++)
                {
                    string name = stream.ReadString();
                    // `Remove` returns true if the node was present
                    if (updates.Remove(name, out var update))
                    {
                        // Validates midi, mogg, image, and milo write dates
                        if (!update.Validate(stream))
                        {
                            AddInvalidSong(name);
                        }
                    }
                    else
                    {
                        AddInvalidSong(name);
                        SongUpdate.SkipRead(stream);
                    }
                }

                // Anything left in the dictionary may require invalidation of cached entries
                foreach (var leftover in updates.Keys)
                {
                    AddInvalidSong(leftover);
                }
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                SongUpdate.SkipRead(stream);
            }
        }

        /// <summary>
        /// Reads a section of the cache containing a list of upgrades to apply from a specific directory,
        /// performing validations on each upgrade node. If an upgrade node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of upgrades</param>
        protected void ReadUpgradeDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var dtaLastWritten = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            // Functions as a "check base directory" call
            if (GetBaseIniGroup(directory) == null)
            {
                goto Invalidate;
            }

            var dirInfo = new DirectoryInfo(directory);
            if (!dirInfo.Exists)
            {
                goto Invalidate;
            }

            var collection = new FileCollection(dirInfo);
            if (!collection.Subfiles.TryGetValue(SONGUPGRADES_DTA, out var dta))
            {
                // We don't *mark* the directory to allow the "New Entries" process
                // to access this collection
                collectionCache.Add(directory, collection);
                goto Invalidate;
            }

            FindOrMarkDirectory(directory);

            // Will add the upgrade group to the shared list on success
            var group = CreateUpgradeGroup(in collection, dta);
            if (group != null && dta.LastWriteTime == dtaLastWritten)
            {
                ValidateUpgrades(group.Upgrades, count, stream);
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                stream.Position += SIZEOF_DATETIME;
            }
        }

        /// <summary>
        /// Reads a section of the cache containing a list of upgrades to apply from a packed CON file,
        /// performing validations on each upgrade node. If an upgrade node from the cache is invalidated, it will mark
        /// any RBCON entry nodes that share its DTA name as invalid, forcing re-evaluation.
        /// </summary>
        /// <param name="stream">The stream containing the list of upgrades</param>
        protected void ReadUpgradeCON(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var conLastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            int count = stream.Read<int>(Endianness.Little);

            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                goto Invalidate;
            }

            // Will add the packed CON group to the shared list on success
            var group = CreateCONGroup(filename, baseGroup.Directory);
            if (group != null && group.Info.LastUpdatedTime == conLastUpdated)
            {
                ValidateUpgrades(group.Upgrades, count, stream);
                return;
            }

        Invalidate:
            for (int i = 0; i < count; i++)
            {
                AddInvalidSong(stream.ReadString());
                stream.Position += SIZEOF_DATETIME;
            }
        }

        /// <summary>
        /// Helper function that runs validation on all the upgrade nodes present within either an upgrade directory or
        /// upgrade CON section of a cache file.
        /// </summary>
        /// <typeparam name="TUpgrade">Type of upgrade node (packed or unpacked)</typeparam>
        /// <param name="groupUpgrades">Dictionary containing the upgrade nodes freshly loaded from the source directory or CON</param>
        /// <param name="count">The number of upgrade nodes present in the cache file</param>
        /// <param name="stream">Stream containing the upgrade node entries</param>
        private void ValidateUpgrades<TUpgrade>(Dictionary<string, (YARGTextContainer<byte> Container, TUpgrade Upgrade)> groupUpgrades, int count, UnmanagedMemoryStream stream)
            where TUpgrade : RBProUpgrade
        {
            // All we need to compare are the last update times
            var upgrades = new Dictionary<string, DateTime>();
            upgrades.EnsureCapacity(groupUpgrades.Count);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastUpdated = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                upgrades.Add(name, lastUpdated);
            }

            foreach (var node in groupUpgrades)
            {
                // `Remove` returns true if the node was present
                if (upgrades.Remove(node.Key, out var dateTime) && node.Value.Upgrade.LastUpdatedTime == dateTime)
                {
                    // Upgrade nodes need to exist before adding CON entries, so we must have a separate list for
                    // all upgrade nodes processed from cache
                    AddCacheUpgrade(node.Key, node.Value.Upgrade);
                }
                else
                {
                    AddInvalidSong(node.Key);
                }
            }

            // Anything left in the dictionary may require invalidation of cached entries
            foreach (var leftover in upgrades.Keys)
            {
                AddInvalidSong(leftover);
            }
        }

        /// <summary>
        /// Attempts to load a PackedCONGroup instance from the validation data present in the cache.
        /// Even if a group is generated however, if the last-update time in the cache does not match what we
        /// receive from the filesystem, this function will not return that instance. Therefore, the caller can not parse
        /// any of the accompanying CON entries that follow the validation data.
        /// </summary>
        /// <param name="stream">Stream containing the CON validation information</param>
        /// <returns>The packed CON group on success; <see langword="null"/> otherwise</returns>
        protected PackedCONGroup? ReadCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var baseGroup = GetBaseIniGroup(filename);
            if (baseGroup == null)
            {
                return null;
            }

            var conLastUpdate = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            var group = FindCONGroup(filename) ?? CreateCONGroup(filename, baseGroup.Directory);
            return group != null && group.Info.LastUpdatedTime == conLastUpdate ? group : null;
        }

        /// <summary>
        /// Attempts to load a UnpackedCONGroup instance from the validation data present in the cache.
        /// Even if a group is generated however, if the last-update time of the songs.dta file in the cache does not match what we
        /// receive from the filesystem, this function will not return that instance. Therefore, the caller can not parse
        /// any of the accompanying CON entries that follow the validation data.
        /// </summary>
        /// <param name="stream">Stream containing the song.dta validation information</param>
        /// <returns>The unpacked CON group on success; <see langword="null"/> otherwise</returns>
        protected UnpackedCONGroup? ReadExtractedCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            var baseGroup = GetBaseIniGroup(directory);
            if (baseGroup == null)
            {
                return null;
            }

            var dtaInfo = new FileInfo(Path.Combine(directory, "songs.dta"));
            if (!dtaInfo.Exists)
            {
                return null;
            }

            FindOrMarkDirectory(directory);

            string playlist = ConstructPlaylist(directory, baseGroup.Directory);
            var group = CreateUnpackedCONGroup(directory, dtaInfo, playlist);
            if (group == null)
            {
                return null;
            }

            var dtaLastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            return dtaInfo.LastWriteTime == dtaLastWrite ? group : null;
        }

        /// <summary>
        /// Loads all the upgrade nodes present in the cache from an "upgrades folder" section
        /// </summary>
        /// <param name="stream">Stream containing the data for a folder's upgrade nodes</param>
        protected void QuickReadUpgradeDirectory(UnmanagedMemoryStream stream)
        {
            string directory = stream.ReadString();
            stream.Position += sizeof(long); // Can skip the last update time
            int count = stream.Read<int>(Endianness.Little);

            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                string filename = Path.Combine(directory, $"{name}_plus.mid");

                var info = new AbridgedFileInfo(filename, stream);
                // Upgrade nodes need to exist before adding CON entries, so we must have a separate list for
                // all upgrade nodes processed from cache
                AddCacheUpgrade(name, new UnpackedRBProUpgrade(info));
            }
        }

        /// <summary>
        /// Loads all the upgrade nodes present in the cache from an "upgrade CON" section.
        /// </summary>
        /// <param name="stream">Stream containing the data for a CON's upgrade nodes</param>
        protected void QuickReadUpgradeCON(UnmanagedMemoryStream stream)
        {
            var listings = QuickReadCONGroupHeader(stream);
            int count = stream.Read<int>(Endianness.Little);
            for (int i = 0; i < count; i++)
            {
                string name = stream.ReadString();
                var lastWrite = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
                var listing = default(CONFileListing);
                listings?.TryGetListing($"songs_upgrades/{name}_plus.mid", out listing);
                // Upgrade nodes need to exist before adding CON entries, so we must have a separate list for
                // all upgrade nodes processed from cache
                AddCacheUpgrade(name, new PackedRBProUpgrade(listing, lastWrite));
            }
        }

        /// <summary>
        /// Attempts to load a list of CONFileListings from a CON file off the filesystem.
        /// The listings that get loaded will utilize the last write information from the cache file,
        /// rather than the last write info off the filesystem.
        /// </summary>
        /// <param name="stream">Stream containing the CON validation information</param>
        /// <returns>A list of CONFileListings on success; <see langword="null"/> otherwise</returns>
        protected List<CONFileListing>? QuickReadCONGroupHeader(UnmanagedMemoryStream stream)
        {
            string filename = stream.ReadString();
            var info = new AbridgedFileInfo(filename, stream);
            if (!File.Exists(filename))
            {
                return null;
            }

            using var filestream = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            return CONFile.TryParseListings(in info, filestream);
        }

        /// <summary>
        /// Attempts to read an unpacked .ini entry from the cache - with all validation checks.
        /// If successful, we mark the directory as "already processed" so that the "New Entries" step doesn't attempt to access it.
        /// </summary>
        /// <param name="directory">The directory potentially containing an entry</param>
        /// <param name="stream">The stream containing the entry's information</param>
        /// <param name="strings">Container will the basic metadata arrays</param>
        /// <returns>An unpacked ini entry on success; <see langword="null"/> otherwise</returns>
        private UnpackedIniEntry? ReadUnpackedIniEntry(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var entry = UnpackedIniEntry.TryLoadFromCache(directory, stream, strings);
            if (entry == null)
            {
                return null;
            }
            FindOrMarkDirectory(directory);
            return entry;
        }

        /// <summary>
        /// Attempts to read an unpacked .sng entry from the cache - with all validation checks.
        /// If successful, we mark the file as "already processed" so that the "New Entries" step doesn't attempt to access it.
        /// </summary>
        /// <param name="fullname">The file path to a potential entry</param>
        /// <param name="stream">The stream containing the entry's information</param>
        /// <param name="strings">Container will the basic metadata arrays</param>
        /// <returns>An packed sng entry on success; <see langword="null"/> otherwise</returns>
        private SngEntry? ReadSngEntry(string fullname, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var entry = SngEntry.TryLoadFromCache(fullname, stream, strings);
            if (entry == null)
            {
                return null;
            }
            FindOrMarkFile(fullname);
            return entry;
        }

        /// <summary>
        /// Creates an UpdateGroup... self-explanatory
        /// </summary>
        /// <param name="collection">The collection of subdirectories and subfiles to locate updates from</param>
        /// <param name="dta">The file info for the main DTA</param>
        /// <returns>An UpdateGroup instance on success; <see langword="null"/> otherwise</returns>
        private UpdateGroup? CreateUpdateGroup(in FileCollection collection, FileInfo dta)
        {
            UpdateGroup? group = null;
            try
            {
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var data = FixedArray<byte>.Load(dta.FullName);
                var updates = new Dictionary<string, SongUpdate>();
                var container = YARGDTAReader.TryCreate(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    if (!updates.TryGetValue(name, out var update))
                    {
                        // We only need to check for the files one time per-DTA name
                        AbridgedFileInfo? midi = null;
                        AbridgedFileInfo? mogg = null;
                        AbridgedFileInfo? milo = null;
                        AbridgedFileInfo? image = null;

                        string subname = name.ToLowerInvariant();
                        if (collection.SubDirectories.TryGetValue(subname, out var directory))
                        {
                            string midiName = subname + "_update.mid";
                            string moggName = subname + "_update.mogg";
                            string miloName = subname + ".milo_xbox";
                            string imageName = subname + "_keep.png_xbox";
                            // Enumerating through the available files through the DirectoryInfo instance
                            // provides a speed boost over manual `File.Exists` checks
                            foreach (var file in directory.EnumerateFiles("*", SearchOption.AllDirectories))
                            {
                                string filename = file.Name;
                                if (filename == midiName)
                                {
                                    midi = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == moggName)
                                {
                                    mogg = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == miloName)
                                {
                                    milo = new AbridgedFileInfo(file, false);
                                }
                                else if (filename == imageName)
                                {
                                    image = new AbridgedFileInfo(file, false);
                                }
                            }
                        }
                        updates.Add(name, update = new SongUpdate(in midi, in mogg, in milo, in image));
                    }
                    // Updates may contain multiple entries for the same DTA name, so we must collect them all under
                    // the same node. However, we won't actually load the information unless later stages require it.
                    update.Containers.Add(container);
                    YARGDTAReader.EndNode(ref container);
                }

                if (updates.Count > 0)
                {
                    // We transfer ownership of the FixedArray data to give the group responsibility over the disposal.
                    // Otherwise, the `using` call would dispose of the data after the scope of this function.
                    group = new UpdateGroup(collection.Directory.FullName, dta.LastWriteTime, data.TransferOwnership(), updates);
                    AddUpdateGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return group;
        }

        /// <summary>
        /// Creates an UpgradeGroup... self-explanatory
        /// </summary>
        /// <param name="collection">The collection of subdirectories and subfiles to locate upgrades from</param>
        /// <param name="dta">The file info for the main DTA</param>
        /// <returns>An UpgradeGroup instance on success; <see langword="null"/> otherwise</returns>
        private UpgradeGroup? CreateUpgradeGroup(in FileCollection collection, FileInfo dta)
        {
            UpgradeGroup? group = null;
            try
            {
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var data = FixedArray<byte>.Load(dta.FullName);
                var upgrades = new Dictionary<string, (YARGTextContainer<byte> Container, UnpackedRBProUpgrade Upgrade)>();
                var container = YARGDTAReader.TryCreate(data);
                while (YARGDTAReader.StartNode(ref container))
                {
                    string name = YARGDTAReader.GetNameOfNode(ref container, true);
                    var upgrade = default(UnpackedRBProUpgrade);
                    // If there is no upgrade file accompanying the DTA node, there's no point in adding the upgrade
                    if (collection.Subfiles.TryGetValue($"{name.ToLower()}_plus.mid", out var info))
                    {
                        var abridged = new AbridgedFileInfo(info, false);
                        upgrade = new UnpackedRBProUpgrade(abridged);
                        upgrades[name] = (container, upgrade);
                    }
                    YARGDTAReader.EndNode(ref container);
                }

                if (upgrades.Count > 0)
                {
                    // We transfer ownership of the FixedArray data to give the group responsibility over the disposal.
                    // Otherwise, the `using` call would dispose of the data after the scope of this function.
                    group = new UpgradeGroup(collection.Directory.FullName, dta.LastWriteTime, data.TransferOwnership(), upgrades);
                    AddUpgradeGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return group;
        }

        /// <summary>
        /// Attempts to create a PackedCONGroup from the file at the provided path.
        /// </summary>
        /// <param name="filename">The path for the file</param>
        /// <param name="baseDirectory">One of the base directories provided by the user</param>
        /// <returns>A PackedCONGroup instance on success; <see langword="null"/> otherwise</returns>
        private PackedCONGroup? CreateCONGroup(string filename, string baseDirectory)
        {
            var info = new FileInfo(filename);
            if (!info.Exists)
            {
                return null;
            }

            FindOrMarkFile(filename);

            string playlist = ConstructPlaylist(filename, baseDirectory);
            var abridged = new AbridgedFileInfo(info);
            return CreateCONGroup(in abridged, playlist);
        }

        /// <summary>
        /// Attempts to create a PackedCONGroup with the provided fileinfo.
        /// </summary>
        /// <param name="info">The file info for the possible CONFile</param>
        /// <param name="defaultPlaylist">The playlist to use for any entries generated from the CON (if it is one)</param>
        /// <returns>A PackedCONGroup instance on success; <see langword="null"/> otherwise</returns>
        private PackedCONGroup? CreateCONGroup(in AbridgedFileInfo info, string defaultPlaylist)
        {
            const string SONGSFILEPATH = "songs/songs.dta";
            const string UPGRADESFILEPATH = "songs_upgrades/upgrades.dta";
            PackedCONGroup? group = null;
            // Holds the file that caused an error in some form
            string errorFile = string.Empty;
            try
            {
                using var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                var listings = CONFile.TryParseListings(in info, stream);
                if (listings == null)
                {
                    return null;
                }

                var songNodes = new Dictionary<string, List<YARGTextContainer<byte>>>();
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var songDTAData = listings.TryGetListing(SONGSFILEPATH, out var songDTA) ? songDTA.LoadAllBytes(stream) : FixedArray<byte>.Null;
                if (songDTAData.IsAllocated)
                {
                    errorFile = SONGSFILEPATH;
                    var container = YARGDTAReader.TryCreate(songDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (!songNodes.TryGetValue(name, out var list))
                        {
                            songNodes.Add(name, list = new List<YARGTextContainer<byte>>());
                        }
                        list.Add(container);
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                var upgrades = new Dictionary<string, (YARGTextContainer<byte> Container, PackedRBProUpgrade Upgrade)>();
                // We call `using` to ensure the proper disposal of data if an error occurs
                using var upgradeDTAData = listings.TryGetListing(UPGRADESFILEPATH, out var upgradeDta) ? upgradeDta.LoadAllBytes(stream) : FixedArray<byte>.Null;
                if (upgradeDTAData.IsAllocated)
                {
                    errorFile = UPGRADESFILEPATH;
                    var container = YARGDTAReader.TryCreate(upgradeDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (listings.TryGetListing($"songs_upgrades/{name}_plus.mid", out var listing))
                        {
                            var upgrade = new PackedRBProUpgrade(listing, listing.LastWrite);
                            upgrades[name] = (container, upgrade);
                        }
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                if (songNodes.Count > 0 || upgrades.Count > 0)
                {
                    // We transfer ownership of the FixedArray data to give the group responsibility over the disposal.
                    // Otherwise, the `using` calls would dispose of the data after the scope of this function.
                    group = new PackedCONGroup(listings, songDTAData.TransferOwnership(), upgradeDTAData.TransferOwnership(), songNodes, upgrades, in info, defaultPlaylist);
                    AddPackedCONGroup(group);
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {info.FullName} - {errorFile}");
            }
            return group;
        }

        /// <summary>
        /// Attempts to create an UnpackedCONGroup from the file at the provided path.
        /// </summary>
        /// <param name="directory">The directory containing the list of entry subdirectories and the main DTA</param>
        /// <param name="dta">The info for the main DTA file</param>
        /// <param name="defaultPlaylist">The playlist to use for any entries generated from the CON (if it is one)</param>
        /// <returns>An UnpackedCONGroup instance on success; <see langword="null"/> otherwise</returns>
        private UnpackedCONGroup? CreateUnpackedCONGroup(string directory, FileInfo dta, string defaultPlaylist)
        {
            try
            {
                using var songDTAData = FixedArray<byte>.Load(dta.FullName);

                var songNodes = new Dictionary<string, List<YARGTextContainer<byte>>>();
                if (songDTAData.IsAllocated)
                {
                    var container = YARGDTAReader.TryCreate(songDTAData);
                    while (YARGDTAReader.StartNode(ref container))
                    {
                        string name = YARGDTAReader.GetNameOfNode(ref container, true);
                        if (!songNodes.TryGetValue(name, out var list))
                        {
                            songNodes.Add(name, list = new List<YARGTextContainer<byte>>());
                        }
                        list.Add(container);
                        YARGDTAReader.EndNode(ref container);
                    }
                }

                if (songNodes.Count > 0)
                {
                    var abridged = new AbridgedFileInfo(dta);
                    var group = new UnpackedCONGroup(songDTAData.TransferOwnership(), songNodes, directory, in abridged, defaultPlaylist);
                    AddUnpackedCONGroup(group);
                    return group;
                }
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex, $"Error while loading {dta.FullName}");
            }
            return null;
        }

        /// <summary>
        /// Constructs a directory-based playlist based on the provided file name 
        /// </summary>
        /// <param name="filename">The path for the current file</param>
        /// <param name="baseDirectory">One of the base directories provided by the user</param>
        /// <returns>The default playlist to potentially use</returns>
        private string ConstructPlaylist(string filename, string baseDirectory)
        {
            string directory = Path.GetDirectoryName(filename);
            if (directory.Length == baseDirectory.Length)
            {
                return "Unknown Playlist";
            }

            if (!fullDirectoryPlaylists)
            {
                return Path.GetFileName(directory);
            }
            return directory[(baseDirectory.Length + 1)..];
        }
        #endregion
    }

    internal static class UnmanagedStreamSlicer
    {
        /// <summary>
        /// Splice a section of the unmanaged stream into a separate instance.
        /// Useful for parallelization and for potentially catching "end of stream" errors.
        /// </summary>
        /// <param name="stream">The base stream to slice</param>
        /// <param name="length">The amount of the data to slice out from the base stream</param>
        /// <returns>The new stream containing the slice</returns>
        public static unsafe UnmanagedMemoryStream Slice(this UnmanagedMemoryStream stream, int length)
        {
            if (stream.Position > stream.Length - length)
            {
                throw new EndOfStreamException();
            }

            var newStream = new UnmanagedMemoryStream(stream.PositionPointer, length);
            stream.Position += length;
            return newStream;
        }
    }

    internal static class AudioFinder
    {
        public static bool ContainsAudio(this SngFile sngFile)
        {
            foreach (var file in sngFile)
            {
                if (IniAudio.IsAudioFile(file.Key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
