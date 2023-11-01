using System;
using System.IO;
using System.Linq;
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
                Debug.LogError("Failed to create LiteDB connection.");
                Debug.LogException(e);
            }
        }

        private static void InitDatabase()
        {
            _db.CreateTable<ScoreEntry>();
        }

        public static void RecordScore(ScoreEntry scoreEntry)
        {
            // Insert the entry
            _db.Insert(scoreEntry);
        }

        public static ScoreInfo? GetHighScore(HashWrapper songChecksum)
        {
            var allTimePlays = _db.Table<ScoreEntry>().Where(i => i.SongChecksum == songChecksum.ToString());

            var scores = allTimePlays
                .SelectMany(i => i.PlayerScores)
                .ToArray();

            // No scores, return null
            if (scores.Length == 0) return null;

            var highScore = scores[0];
            for (int i = 1; i < scores.Length; i++)
            {
                var score = scores[i];
                if (score.Score > highScore.Score)
                {
                    highScore = score;
                }
            }

            return highScore;
        }

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}