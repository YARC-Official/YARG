using System;
using System.IO;
using System.Linq;
using LiteDB;
using UnityEngine;
using YARG.Core.Song;
using YARG.Helpers;

namespace YARG.Scores
{
    public static class ScoreContainer
    {
        public static string ScoreDirectory { get; private set; }
        private static string _scoreDatabaseFile;

        private static LiteDatabase _db;
        private static ILiteCollection<ScoreEntry> _scoreCollection;

        public static void Init()
        {
            ScoreDirectory = Path.Combine(PathHelper.PersistentDataPath, "scores");
            _scoreDatabaseFile = Path.Combine(ScoreDirectory, "scores.db");

            Directory.CreateDirectory(ScoreDirectory);
            try
            {
                _db = new LiteDatabase(_scoreDatabaseFile);
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
            _scoreCollection = _db.GetCollection<ScoreEntry>("scores");

            // These things are commonly queried so they should be indexed
            _scoreCollection.EnsureIndex(i => i.SongChecksum);
            _scoreCollection.EnsureIndex(i => i.Date);
            _scoreCollection.EnsureIndex(i => i.BandScore);
        }

        public static void RecordScore(ScoreEntry scoreEntry)
        {
            // If the score collection does not exist, that means the database errored
            if (_scoreCollection is null) return;

            // Insert the entry
            _scoreCollection.Insert(scoreEntry);

            // TODO: High score cache
        }

        public static ScoreInfo? GetHighScore(HashWrapper songChecksum)
        {
            // If the score collection does not exist, that means the database errored
            if (_scoreCollection is null) return null;

            var allTimePlays = _scoreCollection.Find(Query.EQ("SongChecksum", songChecksum.ToString()));

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