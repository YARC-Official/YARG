using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.IO.Disposables;
using YARG.Core.Logging;
using YARG.Core.Replays;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay.Player;
using YARG.Helpers;

namespace YARG.Replays
{
    public static class ReplayContainer
    {
        /// <summary>
        /// The date revision of the cache format, relative to UTC.
        /// Format is YY_MM_DD_RR: Y = year, M = month, D = day, R = revision (reset across dates, only increment
        /// if multiple cache version changes happen in a single day).
        /// </summary>
        private const int CACHE_VERSION = 24_09_05_01;

        public static string ReplayDirectory { get; private set; }

        private static string _replayCacheFile;

        private static Dictionary<HashWrapper, ReplayInfo> _replayHashMap = new();
        private static Dictionary<string, ReplayInfo> _replayFileMap = new();

        private static FileSystemWatcher _watcher;

        public static Dictionary<HashWrapper, ReplayInfo>.ValueCollection Replays => _replayHashMap.Values;

        public static void Init()
        {
            ReplayDirectory = Path.Combine(PathHelper.PersistentDataPath, "importedReplays");
            _replayCacheFile = Path.Combine(ReplayDirectory, "cache.bin");

            Directory.CreateDirectory(ReplayDirectory);

            _watcher = new FileSystemWatcher(ReplayDirectory, "*.replay")
            {
                EnableRaisingEvents = true,
                IncludeSubdirectories = true
            };

            LoadReplayCache();
            DiscoverReplayFiles();

            _watcher.Created += OnReplayCreated;
            _watcher.Deleted += OnReplayDeleted;
        }

        public static void Destroy()
        {
            _watcher?.Dispose();
            WriteReplayCache();
        }

        public static bool AddEntry(ReplayInfo entry)
        {
            // Ensures the replay is unique
            if (!_replayHashMap.TryAdd(entry.ReplayChecksum, entry))
            {
                return false;
            }

            // Possibly was an overwrite (rare)
            if (!_replayFileMap.TryAdd(entry.FilePath, entry))
            {
                var oldEntry = _replayFileMap[entry.FilePath];
                _replayFileMap[entry.FilePath] = entry;
                _replayHashMap.Remove(oldEntry.ReplayChecksum);
            }
            return true;
        }

        public static bool RemoveReplay(string path)
        {
            if (!_replayFileMap.Remove(path, out var replay))
            {
                return false;
            }
            _replayHashMap.Remove(replay.ReplayChecksum);
            return true;
        }

        public static void LoadReplayCache()
        {
            var info = new FileInfo(_replayCacheFile);
            if (!info.Exists)
            {
                return;
            }

            // If reading the cache fails, clear it and start over

            try
            {
                using var data = MemoryMappedArray.Load(info);
                using var stream = data.ToStream();

                int version = stream.Read<int>(Endianness.Little);
                if (version != CACHE_VERSION)
                {
                    YargLogger.LogFormatWarning("Replay cache version mismatch: {0} != {1}", version, CACHE_VERSION);
                    return;
                }

                int count = stream.Read<int>(Endianness.Little);
                for (int i = 0; i < count; i++)
                {
                    string path = stream.ReadString();
                    var entry = new ReplayInfo(path, stream);
                    if (File.Exists(path))
                    {
                        AddEntry(entry);
                    }
                }
            }
            catch (Exception e)
            {
                _replayHashMap.Clear();
                _replayFileMap.Clear();
                YargLogger.LogException(e, "Failed to load replay cache");
            }
        }

        public static void WriteReplayCache()
        {
            using var stream = File.Open(_replayCacheFile, FileMode.Create);
            using var writer = new NullStringBinaryWriter(stream);

            writer.Write(CACHE_VERSION);
            writer.Write(_replayHashMap.Count);
            foreach (var (path, entry) in _replayFileMap)
            {
                writer.Write(path);
                entry.Serialize(writer);
            }
        }

        private static void DiscoverReplayFiles()
        {
            var directory = new DirectoryInfo(ReplayDirectory);
            foreach (var file in directory.GetFiles("*.replay"))
            {
                if (_replayFileMap.ContainsKey(file.Name))
                {
                    continue;
                }

                var (result, info) = ReplayIO.TryReadMetadata(file.FullName);
                if (result == ReplayReadResult.Valid || result == ReplayReadResult.MetadataOnly)
                {
                    AddEntry(info);
                }
            }

            WriteReplayCache();
        }

        private static void OnReplayCreated(object sender, FileSystemEventArgs e)
        {
            YargLogger.LogFormatDebug("Replay Created: {0}", e.Name);
            if (_replayFileMap.ContainsKey(e.Name))
            {
                return;
            }

            var (result, info)  = ReplayIO.TryReadMetadata(e.FullPath);
            switch (result)
            {
                case ReplayReadResult.Valid:
                    YargLogger.LogFormatDebug("New playable replay: {0}", e.Name);
                    break;
                case ReplayReadResult.MetadataOnly:
                    YargLogger.LogFormatDebug("Out of date replay: {0}", e.Name);
                    break;
                case ReplayReadResult.InvalidVersion:
                    YargLogger.LogFormatWarning("{0} has an invalid replay version.", e.Name);
                    return;
                case ReplayReadResult.NotAReplay:
                    YargLogger.LogFormatDebug("{0} is not a YARG Replay.", e.Name);
                    return;
                case ReplayReadResult.Corrupted:
                    YargLogger.LogFormatWarning("Replay `{0}` is corrupted.", e.Name);
                    return;
                default:
                    throw new InvalidOperationException();
            }

            AddEntry(info);
            WriteReplayCache();
        }

        private static void OnReplayDeleted(object sender, FileSystemEventArgs e)
        {
            YargLogger.LogFormatDebug("Replay Deleted: ", e.Name);
            if (_replayFileMap.TryGetValue(e.Name, out var value))
            {
                RemoveReplay(e.Name);
                WriteReplayCache();
            }
        }
    }
}