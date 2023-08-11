using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using YARG.Core;
using YARG.Core.Engine.Guitar;
using YARG.Core.Replays;
using YARG.Core.Replays.IO;
using YARG.Core.Song;
using YARG.Core.Utility;
using YARG.Gameplay.Player;
using YARG.Helpers;

namespace YARG.Replays
{
    public static class ReplayContainer
    {
        private const int CACHE_VERSION = 23_08_11_1;

        public static string ReplayDirectory { get; private set; }

        private static string _replayCacheFile;

        private static List<ReplayEntry> _replays;
        private static Dictionary<string, ReplayEntry> _replayFileMap;

        private static FileSystemWatcher _watcher;

        public static IReadOnlyList<ReplayEntry> Replays => _replays;

        public static void Init()
        {
            _replays = new List<ReplayEntry>();
            _replayFileMap = new Dictionary<string, ReplayEntry>();

            ReplayDirectory = Path.Combine(PathHelper.PersistentDataPath, "replays");
            _replayCacheFile = Path.Combine(ReplayDirectory, "cache.bin");

            Directory.CreateDirectory(ReplayDirectory);

            _watcher = new FileSystemWatcher(ReplayDirectory, "*.replay")
            {
                EnableRaisingEvents = true, IncludeSubdirectories = true,
            };

            LoadReplayCache();
            DiscoverReplayFiles();

            _watcher.Created += OnReplayCreated;
            _watcher.Deleted += OnReplayDeleted;
        }

        public static void AddReplay(ReplayEntry replay)
        {
            if (!_replays.Contains(replay) && !_replayFileMap.ContainsKey(replay.ReplayFile))
            {
                _replays.Add(replay);
                _replayFileMap.Add(replay.ReplayFile, replay);
            }
        }

        public static void RemoveReplay(ReplayEntry replay)
        {
            if (_replays.Contains(replay) && _replayFileMap.ContainsKey(replay.ReplayFile))
            {
                _replays.Remove(replay);
                _replayFileMap.Remove(replay.ReplayFile);
            }
        }

        public static ReplayReadResult LoadReplayFile(ReplayEntry entry, out Replay replay)
        {
            return ReplayIO.ReadReplay(Path.Combine(ReplayDirectory, entry.ReplayFile), out replay);
        }

        public static Replay CreateNewReplay(SongMetadata song, IList<BasePlayer> players)
        {
            var replay = new Replay
            {
                Header = new ReplayHeader
                {
                    GameVersion = GlobalVariables.CurrentVersion.VersionBits,
                },
                SongName = song.Name,
                ArtistName = song.Artist,
                CharterName = song.Charter,
                SongChecksum = song.Hash,
                Date = DateTime.Now,
                PlayerCount = players.Count,
                PlayerNames = new string[players.Count],
                Frames = new ReplayFrame[players.Count],
            };

            int bandScore = 0;

            for (int i = 0; i < players.Count; i++)
            {
                replay.PlayerNames[i] = players[i].Player.Profile.Name;
                replay.Frames[i] = CreateReplayFrame(i, players[i], out int score);
                bandScore += score;
            }

            replay.BandScore = bandScore;

            return replay;
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

            while (stream.Position < stream.Length)
            {
                var replay = new ReplayEntry
                {
                    SongName = reader.ReadString(),
                    ArtistName = reader.ReadString(),
                    CharterName = reader.ReadString(),
                    BandScore = reader.ReadInt32(),
                    Date = DateTime.FromBinary(reader.ReadInt64()),
                    SongChecksum = new(reader),
                    PlayerCount = reader.ReadInt32()
                };

                replay.PlayerNames = new string[replay.PlayerCount];

                for (int i = 0; i < replay.PlayerNames.Length; i++)
                {
                    replay.PlayerNames[i] = reader.ReadString();
                }

                replay.GameVersion = reader.ReadInt32();
                replay.ReplayFile = reader.ReadString();

                if (File.Exists(Path.Combine(ReplayDirectory, replay.ReplayFile)))
                {
                    AddReplay(replay);
                }
            }
        }

        public static void WriteReplayCache()
        {
            using var stream = File.Open(_replayCacheFile, FileMode.Create);
            using var writer = new NullStringBinaryWriter(stream);

            writer.Write(CACHE_VERSION);
            foreach (var entry in _replays)
            {
                writer.Write(entry.SongName);
                writer.Write(entry.ArtistName);
                writer.Write(entry.CharterName);
                writer.Write(entry.BandScore);
                writer.Write(entry.Date.ToBinary());
                entry.SongChecksum.Serialize(writer);
                writer.Write(entry.PlayerCount);
                for (int i = 0; i < entry.PlayerCount; i++)
                {
                    writer.Write(entry.PlayerNames[i]);
                }

                writer.Write(entry.GameVersion);
                writer.Write(entry.ReplayFile);
            }
        }

        private static void DiscoverReplayFiles()
        {
            var directory = new DirectoryInfo(ReplayDirectory);
            foreach (var file in directory.GetFiles("*.replay"))
            {
                if(_replayFileMap.ContainsKey(file.Name))
                {
                    continue;
                }

                var result = ReplayIO.ReadReplay(file.FullName, out var replay);

                if(result != ReplayReadResult.Valid)
                {
                    continue;
                }

                var entry = new ReplayEntry
                {
                    SongName = replay.SongName,
                    ArtistName = replay.ArtistName,
                    CharterName = replay.CharterName,
                    BandScore = replay.BandScore,
                    Date = replay.Date,
                    SongChecksum = replay.SongChecksum,
                    PlayerCount = replay.PlayerCount,
                    PlayerNames = replay.PlayerNames,
                    GameVersion = replay.Header.GameVersion,
                    ReplayFile = file.Name,
                };

                AddReplay(entry);
            }

            WriteReplayCache();
        }

        private static void OnReplayCreated(object sender, FileSystemEventArgs e)
        {
            Debug.Log("Replay Created: " + e.Name);
            if(_replayFileMap.ContainsKey(e.Name))
            {
                return;
            }

            var result = ReplayIO.ReadReplay(e.FullPath, out var replay);

            switch (result)
            {
                case ReplayReadResult.Valid:
                    Debug.Log($"Read new replay: {e.Name}");
                    break;
                case ReplayReadResult.NotAReplay:
                    Debug.LogWarning($"{e.Name} is not a YARG Replay.");
                    return;
                case ReplayReadResult.InvalidVersion:
                    Debug.LogWarning($"{e.Name} has an invalid replay version.");
                    return;
                case ReplayReadResult.Corrupted:
                    Debug.LogError($"{e.Name} is corrupted.");
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var entry = new ReplayEntry
            {
                SongName = replay.SongName,
                ArtistName = replay.ArtistName,
                CharterName = replay.CharterName,
                BandScore = replay.BandScore,
                Date = replay.Date,
                SongChecksum = replay.SongChecksum,
                PlayerCount = replay.PlayerCount,
                PlayerNames = replay.PlayerNames,
                GameVersion = replay.Header.GameVersion,
                ReplayFile = e.Name,
            };

            AddReplay(entry);
            WriteReplayCache();
        }

        private static void OnReplayDeleted(object sender, FileSystemEventArgs e)
        {
            Debug.Log("Replay Deleted: " + e.Name);
            if(_replayFileMap.TryGetValue(e.Name, out var value))
            {
                RemoveReplay(value);
                WriteReplayCache();
            }
        }

        private static ReplayFrame CreateReplayFrame(int id, BasePlayer player, out int playerScore)
        {
            ReplayFrame frame = null;
            playerScore = 0;
            switch (player.Player.Profile.Instrument.ToGameMode())
            {
                case GameMode.FiveFretGuitar:
                    var fivePlayer = (FiveFretPlayer) player;
                    var fiveFrame = new ReplayFrame<GuitarStats>
                    {
                        Stats = new GuitarStats(fivePlayer.Engine.EngineStats),
                    };

                    playerScore = fiveFrame.Stats.Score;
                    frame = fiveFrame;
                    break;
                case GameMode.SixFretGuitar:
                    break;
                case GameMode.FourLaneDrums:
                    break;
                case GameMode.FiveLaneDrums:
                    break;
                case GameMode.ProGuitar:
                    break;
                case GameMode.Vocals:
                    break;
                default:
                    frame = new ReplayFrame<GuitarStats>();
                    break;
            }

            frame!.PlayerId = id;
            frame.PlayerName = player.Player.Profile.Name;
            frame.Instrument = player.Player.Profile.Instrument;
            frame.Difficulty = player.Player.Profile.Difficulty;
            frame.Inputs = player.ReplayInputs.ToArray();
            frame.InputCount = player.ReplayInputs.Count;

            return frame;
        }
    }
}