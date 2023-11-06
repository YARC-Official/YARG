using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Player;

namespace YARG.Scores
{
    public static class ScoreContainer
    {
        public static string ScoreDirectory { get; private set; }
        private static string _scoreDatabaseFile;

        private static SQLiteConnection _db;

        public static void Init()
        {
            ScoreDirectory = Path.Combine(PathHelper.PersistentDataPath, "scores");
            _scoreDatabaseFile = Path.Combine(ScoreDirectory, "scores.db");

            Directory.CreateDirectory(ScoreDirectory);
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

        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum)
        {
            try
            {
                var query =
                    $"SELECT * FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.Id = GameRecords.Id WHERE " +
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

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}