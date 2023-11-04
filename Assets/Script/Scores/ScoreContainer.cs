using System;
using System.Collections.Generic;
using System.IO;
using SQLite;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers;

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
                    playerEntry.SongChecksum = gameRecord.SongChecksum;
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

        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum)
        {
            try
            {
                var query =
                    $"SELECT * FROM PlayerScores " +
                    $"WHERE SongChecksum = '{songChecksum}' " +
                    $"AND Score = (SELECT MAX(Score) FROM PlayerScores)";
                return _db.FindWithQuery<PlayerScoreRecord>(query);
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to load high score from database. See error below for more details.");
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