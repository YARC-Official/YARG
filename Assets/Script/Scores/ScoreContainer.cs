using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using SQLite;
using UnityEngine;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Song;

namespace YARG.Scores
{
    public static class ScoreContainer
    {
        public static string ScoreDirectory       { get; private set; }
        public static string ScoreReplayDirectory { get; private set; }

        private static string _scoreDatabaseFile;

        private static SQLiteConnection _db;

        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> SongHighScores = new();

        public static void Init()
        {
            ScoreDirectory = Path.Combine(PathHelper.PersistentDataPath, "scores");
            ScoreReplayDirectory = Path.Combine(ScoreDirectory, "replays");

            _scoreDatabaseFile = Path.Combine(ScoreDirectory, "scores.db");

            try
            {
                // Ensure the directories exist
                Directory.CreateDirectory(ScoreDirectory);
                Directory.CreateDirectory(ScoreReplayDirectory);

                _db = new SQLiteConnection(_scoreDatabaseFile);
                InitDatabase();
                FetchHighScores();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to setup ScoreContainer");
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

                YargLogger.LogFormatInfo("Successfully added {0} rows into score database.", rowsAdded);

                var songChecksum = HashWrapper.Create(gameRecord.SongChecksum);

                var bestScore = playerEntries.Find(p => p.Score == playerEntries.Max(x => x.Score));

                if (SongHighScores.TryGetValue(songChecksum, out var highScore))
                {
                    if (bestScore.Score > highScore.Score)
                    {
                        SongHighScores[songChecksum] = bestScore;
                    }
                }
                else
                {
                    SongHighScores.Add(songChecksum, bestScore);
                }

                YargLogger.LogInfo("Recorded high score for song.");
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to add score into database.");
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
                YargLogger.LogException(e, "Failed to load all GameRecords from database.");
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
                YargLogger.LogException(e, "Failed to load all PlayerScoreRecords from database");
            }

            return null;
        }

        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum)
        {
            return SongHighScores?.GetValueOrDefault(songChecksum);

            // If it's not in the high score cache then it's not in the database

            // try
            // {
            //     var query =
            //         $"SELECT * FROM PlayerScores INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id WHERE " +
            //         $"GameRecords.SongChecksum = x'{songChecksum.ToString()}' ORDER BY Score DESC LIMIT 1";
            //     score = _db.FindWithQuery<PlayerScoreRecord>(query);
            //
            //     if(score is not null)
            //     {
            //         SongHighScores.Add(songChecksum, score);
            //     }
            // }
            // catch (Exception e)
            // {
            //     Debug.LogError("Failed to load high score from database. See error below for more details.");
            //     Debug.LogException(e);
            // }
        }

        public static void FetchHighScores()
        {
            try
            {
                const string highScores = "SELECT *, MAX(Score) FROM PlayerScores " +
                    "INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id GROUP BY GameRecords.SongChecksum";
                const string songs = "SELECT Id, SongChecksum FROM GameRecords";

                var scoreResults = _db.Query<PlayerScoreRecord>(highScores);
                var songResults = _db.Query<GameRecord>(songs);

                foreach (var score in scoreResults)
                {
                    var song = songResults.FirstOrDefault(x => x.Id == score.GameRecordId);
                    if (song is null)
                    {
                        continue;
                    }

                    SongHighScores.Add(HashWrapper.Create(song.SongChecksum), score);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load high score from database.");
            }
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
                YargLogger.LogException(e, "Failed to load high score from database.");
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

                var mostPlayed = new List<SongEntry>();
                foreach (var record in playCounts)
                {
                    var hash = HashWrapper.Create(record.SongChecksum);
                    if (SongContainer.SongsByHash.TryGetValue(hash, out var list))
                    {
                        mostPlayed.Add(list.Pick());
                    }
                }

                return mostPlayed;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load most played songs from database.");
            }

            return new List<SongEntry>();
        }

        public static void Destroy()
        {
            _db.Dispose();
        }
    }
}