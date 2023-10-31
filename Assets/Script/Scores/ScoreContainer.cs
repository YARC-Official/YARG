using System;
using System.IO;
using LiteDB;
using UnityEngine;
using YARG.Core.Replays;
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

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}