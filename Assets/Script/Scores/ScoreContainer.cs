using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Helpers.Extensions;
using YARG.Player;
using YARG.Settings;
using YARG.Song;

namespace YARG.Scores
{
    public enum HighScoreHistoryMode
    {
        HighestOverall,
        HighestDifficulty,
    }

    public static partial class ScoreContainer
    {
        public static string ScoreDirectory { get; private set; }
        public static string ScoreReplayDirectory { get; private set; }

        private static string _scoreDatabaseFile;

        private static ScoreDatabase _db;

        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> PlayerHighScores = new();
        private static readonly Dictionary<HashWrapper, PlayerScoreRecord> PlayerHighPercentages = new();
        private static readonly Dictionary<HashWrapper, GameRecord> BandHighScores = new();

        private static Instrument _currentInstrument = Instrument.Band;
        private static Guid _currentPlayerId;

        private static bool HighestDifficultyOnly
            => SettingsManager.Settings.HighScoreHistory.Value == HighScoreHistoryMode.HighestDifficulty;

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

                _db = new ScoreDatabase(_scoreDatabaseFile);
                FetchBandHighScores();
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to setup ScoreContainer");
            }
        }

        public static void Destroy()
        {
            _db.Dispose();
        }

        private static void FetchBandHighScores()
        {
            try
            {
                BandHighScores.Clear();

                var highScores = _db.QueryBandHighScores();
                foreach (var record in highScores)
                {
                    BandHighScores.Add(HashWrapper.Create(record.SongChecksum), record);
                }
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load all GameRecords from database.");
            }
        }

        public static bool IsBandScoreValid(float songSpeed)
        {
            if (!PlayerContainer.Players.Any())
            {
                return false;
            }

            // If any player is disqualified from a valid Solo Score, this should disqualify the Band Score as well.
            if (PlayerContainer.Players.Any(e => !e.SittingOut && !IsSoloScoreValid(songSpeed, e)))
            {
                return false;
            }

            return true;
        }

        public static bool IsSoloScoreValid(float songSpeed, YargPlayer player)
        {
            if (songSpeed < 1.0f || player.Profile.IsBot)
            {
                return false;
            }

            return true;
        }

        public static void RecordScore(GameRecord gameRecord, List<PlayerScoreRecord> playerEntries)
        {
            try
            {
                // Add the game record
                gameRecord.GameVersion = GlobalVariables.Instance.CurrentVersion;
                _db.InsertBandRecord(gameRecord);

                // Assign the proper score entry IDs and checksums
                foreach (var playerEntry in playerEntries)
                {
                    playerEntry.GameRecordId = gameRecord.Id;
                    // Record the player's info in the "Players" table

                    if (!playerEntry.IsReplay)
                    {
                        string name = PlayerContainer.GetProfileById(playerEntry.PlayerId).Name;
                        RecordPlayerInfo(playerEntry.PlayerId, name);
                    }
                }

                _db.InsertSoloRecords(playerEntries);

                // Update cached high scores
                var songChecksum = HashWrapper.Create(gameRecord.SongChecksum);
                UpdateBandHighScore(songChecksum);

                if (playerEntries.Count == 1)
                {
                    // Player high scores are only relevant if there is a single player
                    UpdatePlayerHighScores(songChecksum, playerEntries.First());
                }

                YargLogger.LogInfo("Recorded score for song.");
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to add score into database.");
            }
        }

        private static void UpdateBandHighScore(HashWrapper songChecksum)
        {
            BandHighScores[songChecksum] = _db.QueryBandSongHighScore(songChecksum);
        }

        private static void UpdatePlayerHighScores(HashWrapper songChecksum, PlayerScoreRecord newScore)
        {
            PlayerHighScores[songChecksum] = _db.QueryPlayerSongHighScore(
                songChecksum, newScore.PlayerId, newScore.Instrument, HighestDifficultyOnly);
            PlayerHighPercentages[songChecksum] = _db.QueryPlayerSongHighestPercentage(
                songChecksum, newScore.PlayerId, newScore.Instrument, HighestDifficultyOnly);
        }

        public static void RecordPlayerInfo(Guid id, string name)
        {
            _db.InsertPlayerRecord(id, name);
        }

        public static List<GameRecord> GetAllGameRecords()
        {
            try
            {
                return _db.QueryAllScoresByDate();
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
                return _db.QueryPlayerScores(id);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load all PlayerScoreRecords from database");
            }

            return null;
        }

        /// <summary>
        /// Get the highest score (in points) for a specific song, player and instrument.
        /// If `allowCacheUpdate` is true, all high scores for the player and instrument will be fetched from the database and cached, to speed up subsequent player high score requests.
        /// </summary>
        /// <param name="songChecksum">The ID of the song to retrieve a high score for.</param>
        /// <param name="playerId">The ID of the player profile to retrieve a high score for.</param>
        /// <param name="instrument">The instrument to retrieve a high score for.</param>
        /// <param name="allowCacheUpdate">Sets whether all high scores for this player and instrument should be cached. Set this to true when fetching a large number of high scores for the same player and instrument
        /// (such as when displaying high scores on the Music Library). Set this to false when fetching multiple high scores for different players in a row.</param>
        /// <returns>The highest score for the provided song, player and instrument, or null if no high score exists.</returns>
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
            return BandHighScores.GetValueOrDefault(songChecksum);
        }

        private static PlayerScoreRecord GetHighScoreFromDatabase(HashWrapper songChecksum, Guid playerId, Instrument instrument)
        {
            try
            {
                return _db.QueryPlayerSongHighScore(songChecksum, playerId, instrument, HighestDifficultyOnly);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Failed to load high score from database for player with ID {playerId}.");
                return null;
            }
        }

        /// <summary>
        /// Get the highest score percentage (regardless of points) for a specific song, player and instrument.
        /// Note that this can be different from the highest score in points, as it is possible to get a higher percentage with a lower score.
        /// If `allowCacheUpdate` is true, all high scores for the player and instrument will be fetched from the database and cached, to speed up subsequent player high score requests.
        /// </summary>
        /// <param name="songChecksum">The ID of the song to retrieve a high score for.</param>
        /// <param name="playerId">The ID of the player profile to retrieve a high score for.</param>
        /// <param name="instrument">The instrument to retrieve a high score for.</param>
        /// <param name="allowCacheUpdate">Sets whether all high scores for this player and instrument should be cached. Set this to true when fetching a large number of high scores for the same player and instrument
        /// (such as when displaying high scores on the Music Library). Set this to false when fetching multiple high scores for different players in a row.</param>
        /// <returns>The highest score percentage for the provided song, player and instrument, or null if no high score exists.</returns>
        public static PlayerScoreRecord GetBestPercentageScore(HashWrapper songChecksum, Guid playerId, Instrument instrument, bool allowCacheUpdate = true)
        {
            if (allowCacheUpdate)
            {
                FetchHighScores(playerId, instrument);
            }

            if (_currentInstrument == instrument && _currentPlayerId == playerId)
            {
                return PlayerHighPercentages.GetValueOrDefault(songChecksum);
            }

            return GetHighestPercentageFromDatabase(songChecksum, playerId, instrument);
        }

        private static PlayerScoreRecord GetHighestPercentageFromDatabase(HashWrapper songChecksum, Guid playerId, Instrument instrument)
        {
            try
            {
                return _db.QueryPlayerSongHighestPercentage(songChecksum, playerId, instrument, HighestDifficultyOnly);
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, $"Failed to load high score from database for player with ID {playerId}.");
                return null;
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
                PlayerHighPercentages.Clear();

                var checksums = _db.QuerySongChecksums();

                var highScores = _db.QueryPlayerHighScores(playerId, instrument, HighestDifficultyOnly);
                foreach (var score in highScores)
                {
                    var (_, checksum) = checksums.FirstOrDefault(x => x.RecordId == score.GameRecordId);
                    if (checksum is null)
                    {
                        continue;
                    }

                    PlayerHighScores.Add(HashWrapper.Create(checksum), score);
                }

                var highPercentages = _db.QueryPlayerHighestPercentages(playerId, instrument, HighestDifficultyOnly);
                foreach (var score in highPercentages)
                {
                    var (_, checksum) = checksums.FirstOrDefault(x => x.RecordId == score.GameRecordId);
                    if (checksum is null)
                    {
                        continue;
                    }

                    PlayerHighPercentages.Add(HashWrapper.Create(checksum), score);
                }

                _currentInstrument = instrument;
                _currentPlayerId = playerId;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load high score from database.");
            }
        }

        public static void InvalidateScoreCache()
        {
            _currentPlayerId = Guid.Empty;
            _currentInstrument = Instrument.Band;
        }

        public static List<SongEntry> GetMostPlayedSongs(int maxCount)
        {
            try
            {
                var results = new List<SongEntry>();

                var mostPlayed = _db.QueryMostPlayedSongs(maxCount);
                foreach (var record in mostPlayed)
                {
                    var hash = HashWrapper.Create(record.SongChecksum);
                    if (SongContainer.SongsByHash.TryGetValue(hash, out var list))
                    {
                        results.Add(list.Pick());
                    }
                }

                return results;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load most played songs from database.");
                return new List<SongEntry>();
            }
        }

        // this is the same as GetMostPlayedSongs, but is limited to the one profile and returns the entire list
        public static List<SongEntry> GetPlayedSongsForUserByPlaycount(YargProfile profile, SortOrdering ordering)
        {
            try
            {
                var songList = new List<SongEntry>();

                var mostPlayed = _db.QueryPlayerMostPlayedSongs(profile, ordering);
                foreach (var record in mostPlayed)
                {
                    var hash = HashWrapper.Create(record.SongChecksum);
                    if (SongContainer.SongsByHash.TryGetValue(hash, out var list))
                    {
                        songList.AddRange(list);
                    }
                }

                return songList;
            }
            catch (Exception e)
            {
                YargLogger.LogException(e, "Failed to load most played songs from database.");
                return new List<SongEntry>();
            }
        }
    }
}
