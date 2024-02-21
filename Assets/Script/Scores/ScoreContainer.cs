using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using SQLite;
using UnityEngine;
using YARG.Core;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Player;

namespace YARG.Scores
{
    public static class ScoreContainer
    {
        public static string ScoreDirectory { get; private set; }
        public static string ScoreReplayDirectory { get; private set; }

        private static string _scoreDatabaseFile;

        private static SQLiteConnection _db;

        public static void Init()
        {
            ScoreDirectory = Path.Combine(PathHelper.PersistentDataPath, "scores");
            ScoreReplayDirectory = Path.Combine(ScoreDirectory, "replays");

            _scoreDatabaseFile = Path.Combine(ScoreDirectory, "scores.db");

            // Ensure the directories exist
            Directory.CreateDirectory(ScoreDirectory);
            Directory.CreateDirectory(ScoreReplayDirectory);

            try
            {
                _db = new SQLiteConnection(_scoreDatabaseFile);
                InitDatabase();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to create LiteDB connection. See error below for more details.");
                Debug.LogException(e);
            }
        }

        private static void InitDatabase()
        {
            _db.CreateTable<GameRecord>();
            _db.CreateTable<PlayerScoreRecord>();
            _db.CreateTable<PlayerInfoRecord>();
        }

        public static void RecordScore(GameRecord gameRecord, List<PlayerScoreRecord> playerEntries)
        {
            try
            {
                int rowsAdded = 0;

                // Add the game record
                gameRecord.GameVersion = GlobalVariables.CURRENT_VERSION;
                rowsAdded += _db.Insert(gameRecord);

                // Assign the proper score entry IDs and checksums
                foreach (var playerEntry in playerEntries)
                {
                    playerEntry.GameRecordId = gameRecord.Id;

                    // Record the player's info in the "Players" table
                    string name = PlayerContainer.GetProfileById(playerEntry.PlayerId).Name;
                    RecordPlayerInfo(playerEntry.PlayerId, name);
                }

                rowsAdded += _db.InsertAll(playerEntries);

                Debug.Log($"Successfully added {rowsAdded} rows into score database.");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to add score into database. See error below for more details.");
                Debug.LogException(e);
            }
        }

        public static void RecordPlayerInfo(Guid id, string name)
        {
            var query = $"SELECT * FROM Players WHERE Id = '{id}'";
            var current = _db.FindWithQuery<PlayerInfoRecord>(query);

            if (current is null)
            {
                _db.Insert(new PlayerInfoRecord
                {
                    Id = id,
                    Name = name,
                });
            }
            else
            {
                current.Name = name;
                _db.Update(current);
            }
        }

        public static List<GameRecord> GetAllGameRecords()
        {
            try
            {
                return _db.Query<GameRecord>("SELECT * FROM GameRecords ORDER BY Date DESC");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load all GameRecords from database. See error below for more details.");
                Debug.LogException(e);
            }

            return null;
        }

        public static List<PlayerScoreRecord> GetAllPlayerScores(Guid id)
        {
            try
            {
                return _db.Query<PlayerScoreRecord>($"SELECT * FROM PlayerScores WHERE PlayerId = '{id}'");
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load all PlayerScoreRecords from database. See error below for more details.");
                Debug.LogException(e);
            }

            return null;
        }

        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum)
        {
            try
            {
                var query =
                    $"SELECT * FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id WHERE " +
                    $"GameRecords.SongChecksum = x'{songChecksum.ToString()}' ORDER BY Score DESC LIMIT 1";
                return _db.FindWithQuery<PlayerScoreRecord>(query);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load high score from database. See error below for more details.");
                Debug.LogException(e);
            }

            return null;
        }

        public static PlayerScoreRecord GetHighScoreByInstrument(HashWrapper songChecksum, Instrument instrument)
        {
            try
            {
                var query =
                    $"SELECT * FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id WHERE " +
                    $"GameRecords.SongChecksum = x'{songChecksum.ToString()}' AND PlayerScores.Instrument = {(int) instrument} " +
                    $"ORDER BY Score DESC LIMIT 1";
                return _db.FindWithQuery<PlayerScoreRecord>(query);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load high score from database. See error below for more details.");
                Debug.LogException(e);
            }

            return null;
        }

        public static List<SongEntry> GetMostPlayedSongs(int maxCount)
        {
            try
            {
                var query =
                    $"SELECT SongChecksum, COUNT(SongChecksum) AS `Count` FROM GameRecords GROUP BY SongChecksum ORDER " +
                    $"BY `Count` DESC LIMIT {maxCount}";
                var playCounts = _db.Query<PlayCountRecord>(query);

                var allSongs = GlobalVariables.Instance.SongContainer.SongsByHash;
                var mostPlayed = new List<SongEntry>();
                foreach (var record in playCounts)
                {
                    var hash = new HashWrapper(record.SongChecksum);
                    if (allSongs.TryGetValue(hash, out var list))
                    {
                        mostPlayed.Add(list.Pick());
                    }
                }
                return mostPlayed;
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load most played songs from database. See error below for more details.");
                Debug.LogException(e);
            }

            return new List<SongEntry>();
        }

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}