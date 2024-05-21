using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SQLite;
using YARG.Core;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Menu.MusicLibrary;
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
        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> SongHighScoresByPct = new();

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

                var songChecksum = new HashWrapper(gameRecord.SongChecksum);

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

                var fcFilter = playerEntries.Where(p => p.IsFc == playerEntries.Max(x => x.IsFc));
                var scoreFilter = fcFilter.Where(p => p.Percent == fcFilter.Max(x => x.Percent));
                var bestScoreByPct = scoreFilter.Where(p => p.Difficulty == scoreFilter.Max(x => x.Difficulty)).First();

                if (SongHighScoresByPct.TryGetValue(songChecksum, out var highScoreByPct))
                {
                    if ((bestScoreByPct.IsFc && !highScoreByPct.IsFc)
                    || ((bestScoreByPct.IsFc == highScoreByPct.IsFc) && (bestScoreByPct.Percent > highScoreByPct.Percent))
                    || ((bestScoreByPct.IsFc == highScoreByPct.IsFc) && (bestScoreByPct.Percent == highScoreByPct.Percent) && (bestScoreByPct.Difficulty > highScoreByPct.Difficulty)))
                    {
                        SongHighScoresByPct[songChecksum] = bestScoreByPct;
                    }
                }
                else
                {
                    SongHighScoresByPct.Add(songChecksum, bestScoreByPct);
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

        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum, HighScorePriorityMode highScorePriority = HighScorePriorityMode.Score)
        {
            if (highScorePriority == HighScorePriorityMode.Score)
            {
                return SongHighScores?.GetValueOrDefault(songChecksum);
            }
            else
            {
                return SongHighScoresByPct?.GetValueOrDefault(songChecksum);
            }
        }

        public static void FetchHighScores()
        {
            try
            {
                const string highScores = "SELECT *, MAX(Score) FROM PlayerScores " +
                    "INNER JOIN GameRecords ON PlayerScores.GameRecordId = GameRecords.Id GROUP BY GameRecords.SongChecksum";
                // Tries to pick the best result by looking at IsFc -> Percentage -> Difficulty
                const string highScoresByPct = @"WITH Temp as (SELECT Id, GameRecordId, PlayerId, Instrument,
                                                 Difficulty, EnginePresetId, Score, Stars, NotesHit, NotesMissed, IsFc,
                                                 ifnull(Percent, cast(NotesHit as REAL) / (NotesHit + NotesMissed)) as Percent FROM PlayerScores)
                                                 SELECT Temp.*, max(IsFc * 1000 + cast(Percent * 1000 as INT) + Difficulty * 0.1) as PctSort FROM
                                                 Temp INNER JOIN GameRecords ON Temp.GameRecordId = GameRecords.Id GROUP BY GameRecords.SongChecksum";
                const string highScoresByPctIfNoCol = @"WITH Temp as (SELECT Id, GameRecordId, PlayerId, Instrument,
                                                 Difficulty, EnginePresetId, Score, Stars, NotesHit, NotesMissed, IsFc,
                                                 cast(NotesHit as REAL) / (NotesHit + NotesMissed) as Percent FROM PlayerScores)
                                                 SELECT Temp.*, max(IsFc * 1000 + cast(Percent * 1000 as INT) + Difficulty * 0.1) as PctSort FROM
                                                 Temp INNER JOIN GameRecords ON Temp.GameRecordId = GameRecords.Id GROUP BY GameRecords.SongChecksum";
                const string songs = "SELECT Id, SongChecksum FROM GameRecords";

                var scoreResults = _db.Query<PlayerScoreRecord>(highScores);
                var scoreResultsByPct = ScoresTableHasPercentColumn() ?
                                        _db.Query<PlayerScoreRecord>(highScoresByPct) :
                                        _db.Query<PlayerScoreRecord>(highScoresByPctIfNoCol);
                var songResults = _db.Query<GameRecord>(songs);

                foreach (var score in scoreResults)
                {
                    var song = songResults.FirstOrDefault(x => x.Id == score.GameRecordId);
                    if (song is null)
                    {
                        continue;
                    }

                    SongHighScores.Add(new HashWrapper(song.SongChecksum), score);
                }
                foreach (var score in scoreResultsByPct)
                {
                    var song = songResults.FirstOrDefault(x => x.Id == score.GameRecordId);
                    if (song is null)
                    {
                        continue;
                    }

                    SongHighScoresByPct.Add(new HashWrapper(song.SongChecksum), score);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load high score from database.");
            }
        }

        public class LineCountContainer
        {
            public int Count { get; set; }
        }

        private static bool ScoresTableHasPercentColumn()
        {
            const string checkForPercentColumn = @"SELECT COUNT(*) AS Count FROM pragma_table_info('PlayerScores') WHERE name='Percent'";
            var countResult = _db.Query<LineCountContainer>(checkForPercentColumn);
            return countResult[0].Count == 1;
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
                    var hash = new HashWrapper(record.SongChecksum);
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