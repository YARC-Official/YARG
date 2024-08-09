using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Engine.Drums;
using YARG.Core.Engine.Guitar;
using YARG.Core.Engine.Vocals;
using YARG.Core.Game;
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
        private const int CACHE_VERSION = 23_11_12_01;

        public static IReadOnlyList<ReplayEntry> Replays => _replays;

        public static string ReplayDirectory { get; private set; }

        private static List<ReplayEntry> _replays;

        private static Dictionary<string, ReplayEntry> _replayCache;

        private static FileSystemWatcher _watcher;

        private static string _replayCacheFile;

        public static void Init()
        {
            _replays = new List<ReplayEntry>();
            _replayCache = new Dictionary<string, ReplayEntry>();

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

        public static void LoadReplayCache()
        {
            if (!File.Exists(_replayCacheFile))
            {
                return;
            }

            // If reading the cache fails, clear it and start over

            try
            {
                using var stream = File.OpenRead(_replayCacheFile);
                using var reader = new BinaryReader(stream);

                int version = reader.ReadInt32();
                if (version != CACHE_VERSION)
                {
                    YargLogger.LogFormatWarning("Replay cache version mismatch: {0} != {1}", version, CACHE_VERSION);
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
                        BandStars = (StarAmount) reader.ReadByte(),
                        Date = DateTime.FromBinary(reader.ReadInt64()),
                        SongChecksum = HashWrapper.Deserialize(reader.BaseStream),
                        PlayerCount = reader.ReadInt32()
                    };

                    // Check player count for a limit before allocating a potentially huge array of strings
                    if (replay.PlayerCount > 255)
                    {
                        YargLogger.LogWarning("Replay cache contains a replay with an extremely high player count." +
                            " The replay cache is corrupted as this is not supported.");

                        throw new Exception($"Replay cache corrupted. Player count too high ({replay.PlayerCount})");
                    }

                    replay.PlayerNames = new string[replay.PlayerCount];

                    for (int i = 0; i < replay.PlayerNames.Length; i++)
                    {
                        replay.PlayerNames[i] = reader.ReadString();
                    }

                    replay.EngineVersion = reader.ReadInt32();
                    replay.ReplayPath = reader.ReadString();

                    if (File.Exists(replay.ReplayPath))
                    {
                        AddReplayToCache(replay);
                    }
                }
            }
            catch (Exception e)
            {
                _replays.Clear();
                YargLogger.LogException(e, "Failed to load replay cache");
            }
        }

        public static void AddReplayToCache(ReplayEntry replay)
        {
            if (!_replays.Contains(replay) && !_replayCache.ContainsKey(replay.ReplayPath))
            {
                _replays.Add(replay);
                _replayCache.Add(replay.ReplayPath, replay);
            }
        }

        public static void RemoveReplayFromCache(ReplayEntry replay)
        {
            if (_replays.Contains(replay) && _replayCache.ContainsKey(replay.ReplayPath))
            {
                _replays.Remove(replay);
                _replayCache.Remove(replay.ReplayPath);
            }
        }

        public static Replay CreateNewReplay(SongEntry song, IList<BasePlayer> players, double replayLength)
        {
            var metadata = new ReplayMetadata
            {
                SongName = song.Name,
                ArtistName = song.Artist,
                CharterName = song.Charter,
                ReplayLength = replayLength,
                Date = DateTime.Now,
                SongChecksum = song.Hash,
            };

            var replay = new Replay
            {
                Metadata = metadata,
                PlayerCount = players.Count,
                PlayerNames = new string[players.Count],
                Frames = new ReplayFrame[players.Count],
            };

            int bandScore = 0;
            float bandStars = 0f;

            // Loop through all of the players
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];

                replay.PlayerNames[i] = player.Player.Profile.Name;
                replay.Frames[i] = CreateReplayFrame(i, player);

                bandScore += player.Score;
                bandStars += player.Stars;

                // Make sure preset files are saved
                replay.PresetContainer.StoreColorProfile(player.Player.ColorProfile);
                replay.PresetContainer.StoreCameraPreset(player.Player.CameraPreset);
            }

            metadata.BandScore = bandScore;
            metadata.BandStars = StarAmountHelper.GetStarsFromInt((int) (bandStars / players.Count));

            return replay;
        }

        private static ReplayFrame CreateReplayFrame(int id, BasePlayer player)
        {
            // Create the replay frame with the stats
            var frame = player switch
            {
                FiveFretPlayer fiveFretPlayer => new ReplayFrame
                {
                    Stats = new GuitarStats(fiveFretPlayer.Engine.EngineStats),
                    EngineParameters = fiveFretPlayer.EngineParams
                },
                DrumsPlayer drumsPlayer => new ReplayFrame
                {
                    Stats = new DrumsStats(drumsPlayer.Engine.EngineStats),
                    EngineParameters = drumsPlayer.EngineParams
                },
                VocalsPlayer vocalsPlayer => new ReplayFrame
                {
                    Stats = new VocalsStats(vocalsPlayer.Engine.EngineStats),
                    EngineParameters = vocalsPlayer.EngineParams
                },
                _ => throw new ArgumentOutOfRangeException(player.GetType().ToString(), "Invalid instrument player.")
            };

            // Insert other frame information

            frame!.PlayerInfo = new ReplayPlayerInfo
            {
                PlayerId = id,
                Profile = player.Player.Profile,
            };

            frame.Inputs = player.ReplayInputs.ToArray();
            frame.InputCount = player.ReplayInputs.Count;

            return frame;
        }

        private static void DiscoverReplayFiles()
        {
            var directory = new DirectoryInfo(ReplayDirectory);
            foreach (var file in directory.GetFiles("*.replay"))
            {
                if (_replayCache.ContainsKey(file.FullName))
                {
                    continue;
                }

                var result = ReplayIO.ReadReplay(file.FullName, out var replayFile);

                if (result != ReplayReadResult.Valid)
                {
                    continue;
                }

                var entry = ReplayEntry.CreateFromReplay(replayFile);
                entry.ReplayPath = file.FullName;

                AddReplayToCache(entry);
            }

            WriteReplayCache();
        }

        private static void WriteReplayCache()
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
                writer.Write((byte) entry.BandStars);
                writer.Write(entry.Date.ToBinary());
                entry.SongChecksum.Serialize(writer);

                writer.Write(entry.PlayerCount);
                for (int i = 0; i < entry.PlayerCount; i++)
                {
                    writer.Write(entry.PlayerNames[i]);
                }

                writer.Write(entry.EngineVersion);
                writer.Write(entry.ReplayPath);
            }
        }

        private static void OnReplayCreated(object sender, FileSystemEventArgs e)
        {
            YargLogger.LogFormatDebug("Replay File Created: {0}", e.Name);
            if (_replayCache.ContainsKey(e.FullPath))
            {
                return;
            }

            var result = ReplayIO.ReadReplay(e.FullPath, out var replay);

            switch (result)
            {
                case ReplayReadResult.Valid:
                    YargLogger.LogFormatDebug("Read new replay: {0}", e.Name);
                    break;
                case ReplayReadResult.NotAReplay:
                    YargLogger.LogFormatDebug("{0} is not a YARG Replay.", e.Name);
                    return;
                case ReplayReadResult.InvalidVersion:
                    YargLogger.LogFormatWarning("{0} has an invalid replay version.", e.Name);
                    return;
                case ReplayReadResult.Corrupted:
                    YargLogger.LogFormatWarning("Replay `{0}` is corrupted.", e.Name);
                    return;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var entry = ReplayEntry.CreateFromReplay(replay);
            entry.ReplayPath = e.FullPath;

            AddReplayToCache(entry);
            WriteReplayCache();
        }

        private static void OnReplayDeleted(object sender, FileSystemEventArgs e)
        {
            YargLogger.LogFormatDebug("Replay Deleted: ", e.Name);
            if (_replayCache.TryGetValue(e.FullPath, out var entry))
            {
                RemoveReplayFromCache(entry);
                WriteReplayCache();
            }
        }
    }
}