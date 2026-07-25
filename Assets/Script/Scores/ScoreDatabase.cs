using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SQLite;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;

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
    /// Extended player score record that includes the joined SongChecksum.
    /// </summary>
    public class PlayerScoreWithChecksum : PlayerScoreRecord
    {
        public byte[] SongChecksum { get; set; }
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

            amountUpdated = _db.Execute(
                @"UPDATE GameRecords
                SET HasBots = 0 WHERE HasBots IS NULL"
            );

            if (amountUpdated > 0)
            {
                YargLogger.LogFormatDebug("Successfully updated the HasBots field on {0} rows.", amountUpdated);
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

        private static string BuildInstrumentInClause(IReadOnlyList<Instrument> instruments)
        {
            if (instruments == null || instruments.Count == 0)
            {
                throw new ArgumentException("Instrument list cannot be null or empty.", nameof(instruments));
            }

            return $"IN ({string.Join(", ", Enumerable.Repeat("?", instruments.Count))})";
        }

        private static object[] BuildInstrumentParams(IReadOnlyList<Instrument> instruments)
        {
            var result = new object[instruments.Count];
            for (int i = 0; i < instruments.Count; i++)
            {
                result[i] = (int) instruments[i];
            }
            return result;
        }

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

        public List<PlayerScoreRecord> QueryAllPlayerScoreRecords()
        {
            return Query<PlayerScoreRecord>(
                @"SELECT * FROM PlayerScores
                ORDER BY Id;"
            );
        }


        public List<GameRecord> QueryBandHighScores()
        {
            return Query<GameRecord>(
                @"SELECT gr.* FROM GameRecords gr
                WHERE gr.PlayedWithReplay = 0
                    AND gr.Id = (
                        SELECT gr2.Id FROM GameRecords gr2
                        WHERE gr2.PlayedWithReplay = 0
                            AND gr2.SongChecksum = gr.SongChecksum
                        ORDER BY gr2.BandScore DESC
                        LIMIT 1
                    )"
            );
        }

        public GameRecord QueryBandSongHighScore(HashWrapper songChecksum)
        {
            return FindWithQuery<GameRecord>(
                @"SELECT * FROM GameRecords
                WHERE SongChecksum = ?
                    AND PlayedWithReplay = 0
                ORDER BY BandScore DESC
                LIMIT 1",
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
            bool highestDifficultyOnly,
            bool currentDifficultyOnly,
            Difficulty currentDifficulty
        )
        {
            string orderBy = highestDifficultyOnly
                ? "ps2.Difficulty DESC, ps2.Score DESC"
                : "ps2.Score DESC";
            string difficultyFilter = currentDifficultyOnly ? " AND ps.Difficulty = ?" : "";
            string subDifficultyFilter = currentDifficultyOnly ? " AND ps2.Difficulty = ps.Difficulty" : "";

            string query = $@"SELECT ps.* FROM PlayerScores ps
                INNER JOIN GameRecords gr
                    ON ps.GameRecordId = gr.Id
                WHERE ps.PlayerId = ?
                    AND ps.Instrument = ?
                    {difficultyFilter}
                    AND ps.IsReplay = 0
                    AND ps.Id = (
                        SELECT ps2.Id FROM PlayerScores ps2
                            INNER JOIN GameRecords gr2
                                ON ps2.GameRecordId = gr2.Id
                        WHERE ps2.PlayerId = ps.PlayerId
                            AND ps2.Instrument = ps.Instrument
                            {subDifficultyFilter}
                            AND ps2.IsReplay = 0
                            AND gr2.SongChecksum = gr.SongChecksum
                        ORDER BY {orderBy}
                        LIMIT 1
                    )";

            return currentDifficultyOnly
                ? Query<PlayerScoreRecord>(
                    query,
                    playerId,
                    (int) instrument,
                    (int) currentDifficulty)
                : Query<PlayerScoreRecord>(
                    query,
                    playerId,
                    (int) instrument);
        }

        public List<PlayerScoreRecord> QueryPlayerHighestPercentages(
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly,
            bool currentDifficultyOnly,
            Difficulty currentDifficulty
        )
        {
            string orderBy = highestDifficultyOnly
                ? "ps2.Difficulty DESC, ps2.Percent DESC, ps2.Score DESC, ps2.IsFc DESC"
                : "ps2.Percent DESC, ps2.Score DESC, ps2.IsFc DESC";
            string difficultyFilter = currentDifficultyOnly ? " AND ps.Difficulty = ?" : "";
            string subDifficultyFilter = currentDifficultyOnly ? " AND ps2.Difficulty = ps.Difficulty" : "";

            string query = $@"SELECT ps.* FROM PlayerScores ps
                INNER JOIN GameRecords gr
                    ON ps.GameRecordId = gr.Id
                WHERE ps.PlayerId = ?
                    AND ps.Instrument = ?
                    {difficultyFilter}
                    AND ps.IsReplay = 0
                    AND ps.Id = (
                        SELECT ps2.Id FROM PlayerScores ps2
                            INNER JOIN GameRecords gr2
                                ON ps2.GameRecordId = gr2.Id
                        WHERE ps2.PlayerId = ps.PlayerId
                            AND ps2.Instrument = ps.Instrument
                            {subDifficultyFilter}
                            AND ps2.IsReplay = 0
                            AND gr2.SongChecksum = gr.SongChecksum
                        ORDER BY {orderBy}
                        LIMIT 1
                    )";

            var result = currentDifficultyOnly
                ? Query<PlayerScoreRecord>(
                    query,
                    playerId,
                    (int) instrument,
                    (int) currentDifficulty)
                : Query<PlayerScoreRecord>(
                    query,
                    playerId,
                    (int) instrument);
            return result;
        }

        public PlayerScoreRecord QueryPlayerSongHighScore(
            HashWrapper songChecksum,
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly,
            bool currentDifficultyOnly,
            Difficulty currentDifficulty
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

            if (currentDifficultyOnly)
            {
                query += " AND PlayerScores.Difficulty = ?";
            }

            if (highestDifficultyOnly)
            {
                query += " ORDER BY PlayerScores.Difficulty DESC, PlayerScores.Score DESC";
            }
            else
            {
                query += " ORDER BY PlayerScores.Score DESC";
            }

            query += " LIMIT 1";

            return currentDifficultyOnly
                ? FindWithQuery<PlayerScoreRecord>(
                    query,
                    songChecksum.HashBytes,
                    playerId,
                    (int) instrument,
                    (int) currentDifficulty)
                : FindWithQuery<PlayerScoreRecord>(
                    query,
                    songChecksum.HashBytes,
                    playerId,
                    (int) instrument);
        }

        public PlayerScoreRecord QueryPlayerSongHighestPercentage(
            HashWrapper songChecksum,
            Guid playerId,
            Instrument instrument,
            bool highestDifficultyOnly,
            bool currentDifficultyOnly,
            Difficulty currentDifficulty
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

            if (currentDifficultyOnly)
            {
                query += " AND PlayerScores.Difficulty = ?";
            }

            if (highestDifficultyOnly)
            {
                query += " ORDER BY PlayerScores.Difficulty DESC, PlayerScores.Percent DESC, PlayerScores.Score DESC, IsFc DESC";
            }
            else
            {
                query += " ORDER BY PlayerScores.Percent DESC, PlayerScores.Score DESC, IsFc DESC";
            }

            query += " LIMIT 1";

            return currentDifficultyOnly
                ? FindWithQuery<PlayerScoreRecord>(
                    query,
                    songChecksum.HashBytes,
                    playerId,
                    (int) instrument,
                    (int) currentDifficulty)
                : FindWithQuery<PlayerScoreRecord>(
                    query,
                    songChecksum.HashBytes,
                    playerId,
                    (int) instrument);
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
                @"SELECT GameRecords.SongChecksum, COUNT(GameRecords.Id) AS Count from GameRecords, PlayerScores
                WHERE PlayerScores.GameRecordId = GameRecords.Id
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.IsReplay = 0";

            bool useAggregateDrums = profile.GameMode == GameMode.EliteDrums;

            if (useAggregateDrums)
            {
                query += $" AND PlayerScores.Instrument {BuildInstrumentInClause(MidiDrumkitHelper.Instruments)} ";
            }
            // If the profile instrument is bad, we can still return all scores for the profile
            else if (profile.HasValidInstrument)
            {
                query += " AND PlayerScores.Instrument = ? ";
            }

            query +=
                $@"GROUP BY GameRecords.SongChecksum
                ORDER BY Count {ordering.ToQueryString()}";

            if (useAggregateDrums)
            {
                var parameters = new List<object> { profile.Id };
                parameters.AddRange(BuildInstrumentParams(MidiDrumkitHelper.Instruments));
                return _db.Query<PlayCountRecord>(query, parameters.ToArray());
            }

            if (profile.HasValidInstrument)
            {
                return _db.Query<PlayCountRecord>(
                    query,
                    profile.Id,
                    (int) profile.CurrentInstrument
                );
            }

            return _db.Query<PlayCountRecord>(
                query,
                profile.Id
            );
        }

        public List<PlayerScoreWithChecksum> QueryPlayerBestStars(YargProfile profile, HighScoreHistoryMode mode)
        {
            bool currentDifficultyOnly = mode is
                HighScoreHistoryMode.HighestPercentageCurrentDifficulty or
                HighScoreHistoryMode.HighestScoreCurrentDifficulty;

            string difficultyFilter = currentDifficultyOnly ? " AND ps.Difficulty = ?" : "";
            string subDifficultyFilter = currentDifficultyOnly ? " AND ps2.Difficulty = ps.Difficulty" : "";
            string orderBy = mode switch
            {
                HighScoreHistoryMode.HighestPercentageOverall =>
                    "ps2.Percent DESC, ps2.Score DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestPercentageDifficulty =>
                    "ps2.Difficulty DESC, ps2.Percent DESC, ps2.Score DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestScoreOverall =>
                    "ps2.Score DESC",
                HighScoreHistoryMode.HighestScoreDifficulty =>
                    "ps2.Difficulty DESC, ps2.Score DESC",
                HighScoreHistoryMode.HighestPercentageCurrentDifficulty =>
                    "ps2.Percent DESC, ps2.Score DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestScoreCurrentDifficulty =>
                    "ps2.Score DESC",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            string query = $@"SELECT ps.*, gr.SongChecksum FROM PlayerScores ps
                INNER JOIN GameRecords gr
                    ON ps.GameRecordId = gr.Id
                WHERE ps.PlayerId = ?
                    AND ps.Instrument = ?{difficultyFilter}
                    AND ps.IsReplay = 0
                    AND ps.Id = (
                        SELECT ps2.Id FROM PlayerScores ps2
                            INNER JOIN GameRecords gr2
                                ON ps2.GameRecordId = gr2.Id
                        WHERE ps2.PlayerId = ps.PlayerId
                            AND ps2.Instrument = ps.Instrument{subDifficultyFilter}
                            AND ps2.IsReplay = 0
                            AND gr2.SongChecksum = gr.SongChecksum
                        ORDER BY {orderBy}
                        LIMIT 1
                    )";

            return currentDifficultyOnly
                ? Query<PlayerScoreWithChecksum>(
                    query,
                    profile.Id,
                    (int) profile.CurrentInstrument,
                    (int) profile.CurrentDifficulty)
                : Query<PlayerScoreWithChecksum>(
                    query,
                    profile.Id,
                    (int) profile.CurrentInstrument);
        }

        public List<PlayerScoreWithChecksum> QueryPlayerBestStarsForInstruments(
            YargProfile profile,
            IReadOnlyList<Instrument> instruments,
            HighScoreHistoryMode mode)
        {
            bool currentDifficultyOnly = mode is
                HighScoreHistoryMode.HighestPercentageCurrentDifficulty or
                HighScoreHistoryMode.HighestScoreCurrentDifficulty;

            string inClause = BuildInstrumentInClause(instruments);
            string difficultyFilter = currentDifficultyOnly ? " AND ps.Difficulty = ?" : "";
            string subDifficultyFilter = currentDifficultyOnly ? " AND ps2.Difficulty = ps.Difficulty" : "";
            string orderBy = mode switch
            {
                HighScoreHistoryMode.HighestPercentageOverall =>
                    "ps2.Percent DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestPercentageDifficulty =>
                    "ps2.Difficulty DESC, ps2.Percent DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestScoreOverall =>
                    "ps2.Score DESC",
                HighScoreHistoryMode.HighestScoreDifficulty =>
                    "ps2.Difficulty DESC, ps2.Score DESC",
                HighScoreHistoryMode.HighestPercentageCurrentDifficulty =>
                    "ps2.Percent DESC, ps2.IsFc DESC",
                HighScoreHistoryMode.HighestScoreCurrentDifficulty =>
                    "ps2.Score DESC",
                _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
            };

            string query = $@"SELECT ps.*, gr.SongChecksum FROM PlayerScores ps
                INNER JOIN GameRecords gr
                    ON ps.GameRecordId = gr.Id
                WHERE ps.PlayerId = ?
                    AND ps.Instrument {inClause}{difficultyFilter}
                    AND ps.IsReplay = 0
                    AND ps.Id = (
                        SELECT ps2.Id FROM PlayerScores ps2
                            INNER JOIN GameRecords gr2
                                ON ps2.GameRecordId = gr2.Id
                        WHERE ps2.PlayerId = ps.PlayerId
                            AND ps2.Instrument {inClause}{subDifficultyFilter}
                            AND ps2.IsReplay = 0
                            AND gr2.SongChecksum = gr.SongChecksum
                        ORDER BY {orderBy}
                        LIMIT 1
                    )";

            var parameters = new List<object> { profile.Id };
            parameters.AddRange(BuildInstrumentParams(instruments));
            if (currentDifficultyOnly)
            {
                parameters.Add((int) profile.CurrentDifficulty);
            }
            parameters.AddRange(BuildInstrumentParams(instruments));

            return Query<PlayerScoreWithChecksum>(query, parameters.ToArray());
        }

        public PlayerScoreRecord QueryPlayerSongHighScoreForInstruments(
            HashWrapper songChecksum,
            Guid playerId,
            IReadOnlyList<Instrument> instruments,
            bool highestDifficultyOnly)
        {
            string inClause = BuildInstrumentInClause(instruments);
            string query =
                $@"SELECT * FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE GameRecords.SongChecksum = ?
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.Instrument {inClause}
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

            var parameters = new List<object> { songChecksum.HashBytes, playerId };
            parameters.AddRange(BuildInstrumentParams(instruments));

            return FindWithQuery<PlayerScoreRecord>(query, parameters.ToArray());
        }

        public PlayerScoreRecord QueryPlayerSongHighestPercentageForInstruments(
            HashWrapper songChecksum,
            Guid playerId,
            IReadOnlyList<Instrument> instruments,
            bool highestDifficultyOnly)
        {
            string inClause = BuildInstrumentInClause(instruments);
            string query =
                $@"SELECT * FROM PlayerScores
                INNER JOIN GameRecords
                    ON PlayerScores.GameRecordId = GameRecords.Id
                WHERE GameRecords.SongChecksum = ?
                    AND PlayerScores.PlayerId = ?
                    AND PlayerScores.Instrument {inClause}
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

            var parameters = new List<object> { songChecksum.HashBytes, playerId };
            parameters.AddRange(BuildInstrumentParams(instruments));

            return FindWithQuery<PlayerScoreRecord>(query, parameters.ToArray());
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
