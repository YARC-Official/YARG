using System;
using System.IO;
using LiteDB;
using UnityEngine;
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
            _scoreCollection.EnsureIndex(i => i.SongChecksum, true);
        }

        public static void RecordScore(string songChecksum)
        {
            // If the score collection does not exist, that means the database errored
            if (_scoreCollection is null) return;

            // Create a score entry
            var newEntry = new ScoreEntry
            {
                SongChecksum = songChecksum,
                LastPlayed = DateTime.Now,
                FirstPlayed = DateTime.Now,
                TimesPlayed = 1
            };

            // Get the old entry to update the values (if it exists)
            var oldEntry = _scoreCollection.FindById(songChecksum);
            if (oldEntry is not null)
            {
                newEntry.FirstPlayed = oldEntry.FirstPlayed;
                newEntry.TimesPlayed += oldEntry.TimesPlayed;

                _scoreCollection.Delete(songChecksum);
            }

            // Insert the entry
            _scoreCollection.Insert(newEntry);
        }

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}