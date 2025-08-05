using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
    /// Record for queries that return only checksum information.
    /// </summary>
    /// <remarks>
    /// Note that this is not an actual database record type, it is only used as the result of queries.
    /// </remarks>
    public struct SongRecord
    {
        /// <summary>
        /// The ID of the original <see cref="GameRecord"/>.
        /// </summary>
        public int RecordId;

        /// <summary>
        /// The checksum of the song this record is for.
        /// </summary>
        public byte[] SongChecksum;

        public SongRecord(int id, byte[] checksum)
        {
            RecordId = id;
            SongChecksum = checksum;
        }

        // For tuple deconstruction
        public readonly void Deconstruct(out int id, out byte[] checksum)
        {
            id = RecordId;
            checksum = SongChecksum;
        }
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

            // Default missing IsReplay values to false
            int amountUpdated = _db.Execute(
                @"UPDATE PlayerScores
                SET IsReplay = 0 WHERE IsReplay IS NULL"
            );
            if (amountUpdated > 0)
            {
                YargLogger.LogFormatDebug("Successfully updated the IsReplay field on {0} rows.", amountUpdated);
            }

            // Default missing PlayedWithReplay field to false
            amountUpdated = _db.Execute(
                @"UPDATE GameRecords
                SET PlayedWithReplay = 0 WHERE PlayedWithReplay IS NULL"
            );

            if (amountUpdated > 0)
            {
                YargLogger.LogFormatDebug("Successfully updated the PlayedWithReplay field on {0} rows.", amountUpdated);
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

        private IEnumerable<T> DeferredQuery<T>(string query, params object[] args)
            where T : new()
        {
            YargLogger.LogFormatTrace("Query text:\n{0}", query);
            return _db.DeferredQuery<T>(query, args);
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
            return Query<GameRecord>("SELECT * FROM GameRecords");
        }

        public List<SongRecord> QuerySongChecksums()
        {
            return DeferredQuery<GameRecord>("SELECT Id, SongChecksum FROM GameRecords")
                .Select((record) => new SongRecord(record.Id, record.SongChecksum))
                .ToList();
        }

        public List<GameRecord> QueryAllScoresByDate()
        {
            // We don't check for WasPlayedWithReplay here because this is only used by the history menu
            return Query<GameRecord>(
                @"SELECT * FROM GameRecords
                ORDER BY Date DESC"
            );
        }

        public List<GameRecord> QueryBandHighScores()
        {
            return Query<GameRecord>(
                @"SELECT *, MAX(BandScore) FROM GameRecords
                WHERE PlayedWithReplay = 0
                GROUP BY GameRecords.SongChecksum"
            );
        }

        public GameRecord QueryBandSongHighScore(HashWrapper songChecksum)
        {
            return FindWithQuery<GameRecord>(
                @"SELECT *, MAX(BandScore) FROM GameRecords
                WHERE SongChecksum = ? AND PlayedWithReplay = 0",
                songChecksum.HashBytes
            );
        }

        public List<PlayerScoreRecord> QueryPlayerScores(Guid playerId)
        {
            return Query<PlayerScoreRecord>(
                @"SELECT * FROM PlayerScores
                WHERE PlayerId = ?
                AND IsReplay = 0",
                playerId
            );
        }

        public List<PlayerScoreRecord> QueryPlayerHighScores(
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly
        )
        {
            string difficultyClause = "";
            if (highestDifficultyOnly)
            {
                difficultyClause = "Difficulty DESC,";
            }

            string query = $@"SELECT * FROM (
                SELECT * FROM PlayerScores
                    INNER JOIN GameRecords
                ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE PlayerId = ?
                    AND Instrument = ?
                    AND IsReplay = 0
                ORDER BY {difficultyClause} Score DESC
              )
              GROUP BY SongChecksum";

            return Query<PlayerScoreRecord>(
                query,
                playerId,
                (int) instrument
            );
        }

        public List<PlayerScoreRecord> QueryPlayerHighestPercentages(
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly
        )
        {
            string difficultyClause = "";
            if (highestDifficultyOnly)
            {
                difficultyClause = "Difficulty DESC,";
            }

            string query = $@"SELECT * FROM (
                SELECT * FROM PlayerScores
                    INNER JOIN GameRecords
                ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE PlayerId = ?
                    AND Instrument = ?
                    AND IsReplay = 0
                ORDER BY {difficultyClause} Percent DESC, IsFc DESC
              )
              GROUP BY SongChecksum";

            var result = Query<PlayerScoreRecord>(
                query,
                playerId,
                (int) instrument
            );
            return result;
        }

        public PlayerScoreRecord QueryPlayerSongHighScore(
            HashWrapper songChecksum,
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly
        )
        {
            string query =
                @"SELECT * FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE GameRecords.SongChecksum = ?
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.Instrument = ?
                    AND PlayerScores.IsReplay = 0";

            if (highestDifficultyOnly)
            {
                query += " ORDER BY PlayerScores.Difficulty DESC, PlayerScores.Score DESC";
            }
            else
            {
                query += " ORDER BY PlayerScores.Score DESC";
            }

            query += " LIMIT 1";

            return FindWithQuery<PlayerScoreRecord>(
                query,
                songChecksum.HashBytes,
                playerId,
                (int) instrument
            );
        }

        public PlayerScoreRecord QueryPlayerSongHighestPercentage(
            HashWrapper songChecksum,
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly
        )
        {
            string query =
                @"SELECT * FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE GameRecords.SongChecksum = ?
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.Instrument = ?
                    AND PlayerScores.IsReplay = 0";

            if (highestDifficultyOnly)
            {
                query += " ORDER BY PlayerScores.Difficulty DESC, PlayerScores.Percent DESC, IsFc DESC";
            }
            else
            {
                query += " ORDER BY PlayerScores.Percent DESC, IsFc DESC";
            }

            query += " LIMIT 1";

            return FindWithQuery<PlayerScoreRecord>(
                query,
                songChecksum.HashBytes,
                playerId,
                (int) instrument
            );
        }

        public List<SongRecord> QueryMostPlayedSongs(int maxCount)
        {
            return
                DeferredQuery<GameRecord>(
                    $@"SELECT Id, SongChecksum, COUNT(SongChecksum) AS `Count` FROM GameRecords
                    WHERE PlayedWithReplay = 0
                    GROUP BY SongChecksum
                    ORDER BY `Count` DESC
                    LIMIT {maxCount}"
                )
                .Select((record) => new SongRecord(record.Id, record.SongChecksum))
                .ToList();
        }

        public List<PlayCountRecord> QueryPlayerMostPlayedSongs(YargProfile profile, SortOrdering ordering)
        {
            var query =
                @"SELECT COUNT(GameRecords.Id), GameRecords.SongChecksum from GameRecords, PlayerScores
                WHERE PlayerScores.GameRecordId = GameRecords.Id
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.IsReplay = 0";

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
