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
using YARG.Player;
using YARG.Song;

namespace YARG.Scores
{
    public static partial class ScoreContainer
    {
        public static string ScoreDirectory { get; private set; }
        public static string ScoreReplayDirectory { get; private set; }

        private static string _scoreDatabaseFile;

        private static SQLiteConnection _db;

        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> PlayerHighScores = new();
        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> PlayerHighScoresByPct = new();
        private static readonly Dictionary<HashWrapper, GameRecord> BandHighScores = new();

        private static Instrument _currentInstrument = Instrument.Band;
        private static Guid _currentPlayerId;

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
                UpdateNullPercents();
                FetchBandHighScores();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to setup ScoreContainer");
            }
        }

        private static void FetchBandHighScores()
        {
            try
            {
                BandHighScores.Clear();
                var result = _db.Query<GameRecord>(QUERY_BAND_BEST_SCORES);

                foreach (var record in result)
                {
                    BandHighScores.Add(HashWrapper.Create(record.SongChecksum), record);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load all GameRecords from database.");
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
                gameRecord.GameVersion = GlobalVariables.Instance.CurrentVersion;
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

                if (playerEntries.Count == 1)
                {
                    // Update the cached player high scores. Only relevant if there is a single human player.
                    UpdatePlayerHighScores(songChecksum, playerEntries.First());
                }

                UpdateBandHighScore(songChecksum, gameRecord);

                YargLogger.LogInfo("Recorded high score for song.");
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to add score into database.");
            }
        }

        private static void UpdateBandHighScore(HashWrapper songChecksum, GameRecord currentBandScore)
        {
            if (!BandHighScores.TryGetValue(songChecksum, out var currentBest))
            {
                BandHighScores.Add(songChecksum, currentBandScore);
            }
            if (currentBest.BandScore >= currentBandScore.BandScore)
            {
                return;
            }

            BandHighScores[songChecksum] = currentBandScore;
        }

        private static void UpdatePlayerHighScores(HashWrapper songChecksum, PlayerScoreRecord newScore)
        {

            PlayerHighScoresByPct.TryGetValue(songChecksum, out var currentBestPct);
            if (!PlayerHighScores.TryGetValue(songChecksum, out var currentBest))
            {
                PlayerHighScores.Add(songChecksum, newScore);
                PlayerHighScoresByPct.Add(songChecksum, newScore);
                return;
            }

            if (newScore.Score > currentBest.Score)
            {
                PlayerHighScores[songChecksum] = newScore;

                // If there's a new best score on a different difficulty, the old best percentage entry is irrelevant. Use the new score as the new best percentage entry.
                if (newScore.Difficulty != currentBest.Difficulty || newScore.GetPercent() > currentBest.GetPercent())
                {
                    PlayerHighScoresByPct[songChecksum] = newScore;
                }
            }

            // Update the best percentage entry if appropriate.
            if (newScore.Difficulty == currentBestPct.Difficulty || newScore.GetPercent() > currentBest.GetPercent())
            {
                PlayerHighScoresByPct[songChecksum] = newScore;
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

        /// <summary>
        /// Get the high score for a specific song, player and instrument. If `allowCacheUpdate` is true, all high scores for the player and instrument will be fetched from the database and cached,
        /// to speed up subsequent player high score requests.
        /// </summary>
        /// <param name="songChecksum">The ID of the song to retrieve a high score for.</param>
        /// <param name="playerId">The ID of the player profile to retrieve a high score for.</param>
        /// <param name="instrument">The instrument to retrieve a high score for.</param>
        /// <param name="allowCacheUpdate">Sets whether all high scores for this player and instrument should be cached. Set this to true when fetching a large number of high scores for the same player and instrument
        /// (such as when displaying high scores on the Music Library). Set this to false when fetching multiple high scores for different players in a row.</param>
        /// <returns></returns>
        public static PlayerScoreRecord GetHighScore(HashWrapper songChecksum, Guid playerId, Instrument instrument, bool allowCacheUpdate = true)
        {
            if (allowCacheUpdate)
            {
                FetchHighScores(playerId, instrument);
            }

            if (_currentInstrument == instrument && _currentPlayerId == playerId)
            {
                return PlayerHighScores.GetValueOrDefault(songChecksum);
            }

            return GetHighScoreFromDatabase(songChecksum, playerId, instrument);
        }

        public static GameRecord GetBandHighScore(HashWrapper songChecksum)
        {
            if (BandHighScores.TryGetValue(songChecksum, out var record))
            {
                return record;
            }

            return null;
        }

        private static PlayerScoreRecord GetHighScoreFromDatabase(HashWrapper songChecksum, Guid playerId, Instrument instrument)
        {
            var query = QUERY_SINGLE_PLAYER_HIGH_SCORE;

            try
            {
                return _db.FindWithQuery<PlayerScoreRecord>(query, songChecksum.ToString(), playerId, (int) instrument);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Failed to load high score from database for player with ID {playerId}.");
                return null;
            }
        }

        public static PlayerScoreRecord GetBestPercentageScore(HashWrapper songChecksum, Guid playerId, Instrument instrument, bool allowCacheUpdate = true)
        {
            if (allowCacheUpdate)
            {
                FetchHighScores(playerId, instrument);
            }

            if (_currentInstrument == instrument && _currentPlayerId == playerId)
            {
                return PlayerHighScoresByPct.GetValueOrDefault(songChecksum);
            }

            // TODO: Fetch best % from database if there's a cache miss. Not currently needed.
            throw new NotImplementedException();
        }

        public static void UpdateNullPercents()
        {
            try
            {
                var n = _db.Execute(QUERY_UPDATE_NULL_PERCENTS);
                if (n > 0)
                {
                    YargLogger.LogFormatInfo("Successfully updated the percentage field on {0} rows.", n);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to update null percents in database.");
            }
        }


        private static void FetchHighScores(Guid playerId, Instrument instrument)
        {
            if (_currentPlayerId == playerId && _currentInstrument == instrument && PlayerHighScores.Any())
            {
                // Already cached. No need to fetch again from the database.
                return;
            }

            try
            {
                PlayerHighScores.Clear();
                PlayerHighScoresByPct.Clear();

                var scoreResults = _db.Query<PlayerScoreRecord>(QUERY_HIGH_SCORES, playerId, (int) instrument);
                var songResults = _db.Query<GameRecord>(QUERY_SONGS, playerId, (int) instrument);
                foreach (var score in scoreResults)
                {
                    var song = songResults.FirstOrDefault(x => x.Id == score.GameRecordId);
                    if (song is null)
                    {
                        continue;
                    }

                    PlayerHighScores.Add(HashWrapper.Create(song.SongChecksum), score);
                }

                var scoreResultsByPct = _db.Query<PlayerScoreRecord>(QUERY_BEST_SCORES_BY_PERCENT, playerId, (int) instrument);
                foreach (var score in scoreResultsByPct)
                {
                    var song = songResults.FirstOrDefault(x => x.Id == score.GameRecordId);
                    if (song is null)
                    {
                        continue;
                    }

                    PlayerHighScoresByPct.Add(HashWrapper.Create(song.SongChecksum), score);
                }

                _currentInstrument = instrument;
                _currentPlayerId = playerId;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load high score from database.");
            }
        }

        /*
        [Obsolete]
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
        */

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