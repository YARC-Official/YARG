using System;
using System.Collections;
using System.Collections.Generic;
using SQLite;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;

namespace YARG.Scores
{
    public enum SortOrdering
    {
        Ascending,
        Descending,
    }

    /// <summary>
    /// The score database.
    /// </summary>
    /// <remarks>
    /// Wrapper around raw SQL queries to help ensure proper type safety and parameter positions.
    /// All queries should be implemented as self-contained methods within this class.
    /// </remarks>
    public class ScoreDatabase : IDisposable
    {
        public SQLiteConnection _db;

        public ScoreDatabase(string path)
        {
            _db = new SQLiteConnection(path);

            // Initialize tables
            _db.CreateTable<GameRecord>();
            _db.CreateTable<PlayerScoreRecord>();
            _db.CreateTable<PlayerInfoRecord>();

            // Fill in missing percentage values
            int amountFilled = _db.Execute(
                @"UPDATE PlayerScores
                SET Percent = cast(NotesHit as REAL) / (NotesHit + NotesMissed)
                WHERE Percent IS NULL"
            );
            if (amountFilled > 0)
            {
                YargLogger.LogFormatDebug("Successfully updated the percentage field on {0} rows.", amountFilled);
            }
        }

        public void Dispose()
        {
            _db.Dispose();
        }

        #region Helpers for tracing database operations

        private void Insert(object record)
        {
            int rows = _db.Insert(record);
            YargLogger.LogFormatTrace("Inserted {0} rows into score database.", rows);
        }

        private void InsertAll(IEnumerable record)
        {
            int rows = _db.InsertAll(record);
            YargLogger.LogFormatTrace("Inserted {0} rows into score database.", rows);
        }

        private void Update(object record)
        {
            int rows = _db.Update(record);
            YargLogger.LogFormatTrace("Updated {0} rows in score database.", rows);
        }

        private List<T> Query<T>(string query, params object[] args)
            where T : new()
        {
            YargLogger.LogFormatTrace("Query text:\n{0}", query);
            return _db.Query<T>(query, args);
        }

        private T FindWithQuery<T>(string query, params object[] args)
            where T : new()
        {
            YargLogger.LogFormatTrace("Query text:\n{0}", query);
            return _db.FindWithQuery<T>(query, args);
        }

        #endregion

        #region Explicitly-typed insertions for clarity and type safety

        public void InsertPlayerRecord(Guid playerId, string name)
        {
            var current = FindWithQuery<PlayerInfoRecord>(
                @"SELECT * FROM Players
                WHERE Id = ?",
                playerId
            );

            if (current is null)
            {
                Insert(new PlayerInfoRecord
                {
                    Id = playerId,
                    Name = name,
                });
            }
            else
            {
                current.Name = name;
                Update(current);
            }
        }

        public void InsertBandRecord(GameRecord record)
        {
            Insert(record);
        }

        public void InsertSoloRecords(IEnumerable<PlayerScoreRecord> records)
        {
            InsertAll(records);
        }

        #endregion

        #region Query helper methods

        public List<GameRecord> QueryAllScores()
        {
            return Query<GameRecord>(
                "SELECT Id, SongChecksum FROM GameRecords"
            );
        }

        public List<GameRecord> QueryAllScoresByDate()
        {
            return Query<GameRecord>(
                @"SELECT * FROM GameRecords
                ORDER BY Date DESC"
            );
        }

        public List<GameRecord> QueryBandHighScores()
        {
            return Query<GameRecord>(
                @"SELECT *, MAX(BandScore) FROM GameRecords
                GROUP BY GameRecords.SongChecksum"
            );
        }

        public List<PlayerScoreRecord> QueryPlayerScores(Guid playerId)
        {
            return Query<PlayerScoreRecord>(
                @"SELECT * FROM PlayerScores
                WHERE PlayerId = ?",
                playerId
            );
        }

        public List<PlayerScoreRecord> QueryPlayerHighScores(Guid playerId, Instrument instrument)
        {
            return Query<PlayerScoreRecord>(
                @"SELECT *, MAX(Score) FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE PlayerId = ?
                    AND Instrument = ?
                GROUP BY GameRecords.SongChecksum",
                playerId,
                (int) instrument
            );
        }

        public List<PlayerScoreRecord> QueryPlayerHighScoresByPercent(Guid playerId, Instrument instrument)
        {
            return Query<PlayerScoreRecord>(
                @"
                WITH
                    /* For each song, this retrieves the records with the best score */
                    BestScore as (
                        SELECT *, max(Score) FROM PlayerScores
                        INNER JOIN GameRecords
                            ON PlayerScores.GameRecordId = GameRecords.Id
                        GROUP BY
                            GameRecords.SongChecksum,
                            PlayerScores.Instrument,
                            PlayerScores.PlayerId
                    ),

                    /* For each song, instrument, and difficulty, this retrieves the records with the best score by percent */
                    BestPercents as (
                        SELECT *, max(Percent) FROM PlayerScores
                        INNER JOIN GameRecords
                            ON PlayerScores.GameRecordId = GameRecords.Id
                        GROUP BY
                            GameRecords.SongChecksum,
                            PlayerScores.Instrument,
                            PlayerScores.PlayerId,
                            PlayerScores.Difficulty
                    )

                /*
                    Filter the lines from the BestPercents temporary table above where the instrument and difficulty match
                    the instrument and difficulty when the highest score was recorded
                */
                SELECT BestPercents.* FROM BestPercents
                INNER JOIN BestScore
                    ON BestScore.Instrument = BestPercents.Instrument
                    AND BestScore.Difficulty = BestPercents.Difficulty
                    AND BestScore.SongChecksum = BestPercents.SongChecksum
                    AND BestScore.PlayerId = BestPercents.PlayerId
                WHERE BestScore.PlayerId = ?
                    AND BestScore.Instrument = ?;
                ",
                playerId,
                (int) instrument
            );
        }

        public PlayerScoreRecord QueryPlayerHighestScore(HashWrapper songChecksum, Guid playerId, Instrument instrument)
        {
            return FindWithQuery<PlayerScoreRecord>(
                @"SELECT * FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE GameRecords.SongChecksum = ?
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.Instrument = ?
                ORDER BY Score DESC
                LIMIT 1",
                songChecksum.ToString(),
                playerId,
                (int) instrument
            );
        }

        public List<GameRecord> QueryMostPlayedSongs(int maxCount)
        {
            return Query<GameRecord>(
                $@"SELECT SongChecksum, COUNT(SongChecksum) AS `Count` FROM GameRecords
                GROUP BY SongChecksum
                ORDER BY `Count` DESC
                LIMIT {maxCount}"
            );
        }

        public List<PlayCountRecord> QueryPlayerMostPlayedSongs(YargProfile profile, SortOrdering ordering)
        {
            var query =
                @"SELECT COUNT(GameRecords.Id), GameRecords.SongChecksum from GameRecords, PlayerScores
                WHERE PlayerScores.GameRecordId = GameRecords.Id
                    AND PlayerScores.PlayerId = ?";

            // If the profile instrument is bad, we can still return all scores for the profile
            if (profile.HasValidInstrument)
            {
                query += " AND PlayerScores.Instrument = ? ";
            }

            query +=
                $@"GROUP BY GameRecords.SongChecksum
                ORDER BY COUNT(GameRecords.Id) {ordering.ToQueryString()}";

            return _db.Query<PlayCountRecord>(
                query,
                profile.Id,
                (int) profile.CurrentInstrument
            );
        }

        #endregion
    }

    public static class SortOrderingExtensions
    {
        public static string ToQueryString(this SortOrdering ordering)
        {
            return ordering switch
            {
                SortOrdering.Ascending => "ASC",
                SortOrdering.Descending => "DESC",
                _ => throw new Exception("Invalid ordering"),
            };
        }
    }
}
