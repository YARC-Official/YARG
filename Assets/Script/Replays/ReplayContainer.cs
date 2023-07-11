using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YARG.Core.Replays;
using YARG.Core.Replays.IO;
using YARG.Util;

namespace YARG.Replays
{
    public static class ReplayContainer
    {
        private const int CACHE_VERSION = 23_07_11_1;

        private static List<ReplayEntry> _replays;

        private static string _replayDirectory;
        private static string _replayCacheFile;

        private static FileSystemWatcher _watcher;

        public static void Init()
        {
            _replays = new List<ReplayEntry>();

            _replayDirectory = Path.Combine(PathHelper.PersistentDataPath, "replays");
            _replayCacheFile = Path.Combine(_replayDirectory, "cache.bin");

            Directory.CreateDirectory(_replayDirectory);

            _watcher = new FileSystemWatcher(_replayDirectory, "*.replay")
            {
                EnableRaisingEvents = true, IncludeSubdirectories = true,
            };

            _watcher.Created += OnReplayCreated;
            _watcher.Deleted += OnReplayDeleted;
        }

        public static void AddReplay(ReplayEntry replay)
        {
            if (!_replays.Contains(replay))
            {
                _replays.Add(replay);
            }
        }

        public static void RemoveReplay(ReplayEntry replay)
        {
            if (_replays.Contains(replay))
            {
                _replays.Remove(replay);
            }
        }

        public static ReplayReadResult LoadReplayFile(ReplayEntry entry, out Replay replay)
        {
            return ReplayIO.ReadReplay(entry.ReplayFile, out replay);
        }

        public static void LoadReplayCache()
        {
            if (!File.Exists(_replayCacheFile))
            {
                return;
            }

            using var stream = File.OpenRead(_replayCacheFile);
            using var reader = new BinaryReader(stream);

            int version = reader.ReadInt32();
            if (version != CACHE_VERSION)
            {
                Debug.LogWarning($"Replay cache version mismatch: {version} != {CACHE_VERSION}");
                return;
            }

            while(stream.Position < stream.Length)
            {
                var replay = new ReplayEntry
                {
                    SongName = reader.ReadString(),
                    ArtistName = reader.ReadString(),
                    CharterName = reader.ReadString(),
                    BandScore = reader.ReadInt32(),
                    Date = DateTime.FromBinary(reader.ReadInt64()),
                    SongChecksum = reader.ReadString(),
                    PlayerCount = reader.ReadInt32()
                };

                replay.PlayerNames = new string[replay.PlayerCount];

                for (int i = 0; i < replay.PlayerNames.Length; i++)
                {
                    replay.PlayerNames[i] = reader.ReadString();
                }

                replay.GameVersion = reader.ReadInt32();
                replay.ReplayFile = reader.ReadString();

                _replays.Add(replay);
            }
        }

        public static void WriteReplayCache()
        {
            using var stream = File.OpenWrite(_replayCacheFile);
            using var writer = new BinaryWriter(stream);

            writer.Write(CACHE_VERSION);
            foreach (var entry in _replays)
            {
                writer.Write(entry.SongName);
                writer.Write(entry.ArtistName);
                writer.Write(entry.CharterName);
                writer.Write(entry.BandScore);
                writer.Write(entry.Date.ToBinary());
                writer.Write(entry.SongChecksum);
                writer.Write(entry.PlayerCount);
                for (int i = 0; i < entry.PlayerCount; i++)
                {
                    writer.Write(entry.PlayerNames[i]);
                }
                writer.Write(entry.GameVersion);
                writer.Write(entry.ReplayFile);
            }
        }

        private static void OnReplayCreated(object sender, FileSystemEventArgs e)
        {
            Debug.Log("Created:" + e.Name);
        }

        private static void OnReplayDeleted(object sender, FileSystemEventArgs e)
        {
            Debug.Log("Deleted:" + e.Name);
        }
    }
}