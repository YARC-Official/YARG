using System;
using System.IO;
using NUnit.Framework;
using Shouldly;
using YARG.Core;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Scores;

namespace YARG.UnitTests
{
    [TestFixture]
    public sealed class ScoreDatabaseIntegrationTests
    {
        private static readonly HashWrapper SongChecksumA =
            HashWrapper.FromString("1111111111111111111111111111111111111111");
        private static readonly HashWrapper SongChecksumB =
            HashWrapper.FromString("2222222222222222222222222222222222222222");

        private string _tempDbPath = null!;
        private ScoreDatabase _database = null!;

        [SetUp]
        public void SetUp()
        {
            _tempDbPath = Path.Combine(Path.GetTempPath(), $"yarg_score_test_{Guid.NewGuid():N}.db");
            _database = new ScoreDatabase(_tempDbPath);
        }

        [TearDown]
        public void TearDown()
        {
            _database?.Dispose();
            _database = null!;

            if (File.Exists(_tempDbPath))
            {
                try
                {
                    File.Delete(_tempDbPath);
                }
                catch
                {
                    // Ignored if file handle is releasing
                }
            }
        }

        [Test]
        public void EmptyDatabase_QueriesReturnNullOrEmpty()
        {
            _database.QueryAllScores().ShouldBeEmpty();
            _database.QueryBandHighScores().ShouldBeEmpty();
            _database.QueryBandSongHighScore(SongChecksumA).ShouldBeNull();
            _database.QueryPlayerScores(Guid.NewGuid()).ShouldBeEmpty();
        }

        [Test]
        public void InsertBandRecord_PersistsAndQueriesAllScores()
        {
            var record = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 350000, StarAmount.Star5);

            _database.InsertBandRecord(record);

            var allScores = _database.QueryAllScores();
            allScores.Count.ShouldBe(1);
            allScores[0].BandScore.ShouldBe(350000);
            allScores[0].SongName.ShouldBe("Song A");
            allScores[0].SongArtist.ShouldBe("Artist A");
        }

        [Test]
        public void QueryBandSongHighScore_MultipleRecords_ReturnsHighestScore()
        {
            var lowerRecord = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 250000, StarAmount.Star4);
            var higherRecord = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 450000, StarAmount.StarGold);

            _database.InsertBandRecord(lowerRecord);
            _database.InsertBandRecord(higherRecord);

            var highScore = _database.QueryBandSongHighScore(SongChecksumA);
            highScore.ShouldNotBeNull();
            highScore.BandScore.ShouldBe(450000);
            highScore.BandStars.ShouldBe(StarAmount.StarGold);
        }

        [Test]
        public void QueryBandHighScores_AcrossMultipleSongs_GroupsBySongChecksum()
        {
            var songA1 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 100000, StarAmount.Star3);
            var songA2 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 200000, StarAmount.Star5);
            var songB1 = CreateGameRecord(SongChecksumB, "Song B", "Artist B", 500000, StarAmount.StarGold);

            _database.InsertBandRecord(songA1);
            _database.InsertBandRecord(songA2);
            _database.InsertBandRecord(songB1);

            var bandHighScores = _database.QueryBandHighScores();
            bandHighScores.Count.ShouldBe(2);

            var scoreA = bandHighScores.Find(r => HashWrapper.Create(r.SongChecksum).Equals(SongChecksumA));
            var scoreB = bandHighScores.Find(r => HashWrapper.Create(r.SongChecksum).Equals(SongChecksumB));

            scoreA.ShouldNotBeNull();
            scoreA.BandScore.ShouldBe(200000);

            scoreB.ShouldNotBeNull();
            scoreB.BandScore.ShouldBe(500000);
        }

        [Test]
        public void InsertSoloRecords_AndQueryPlayerSongHighScore_ReturnsBestSoloRun()
        {
            var playerId = Guid.NewGuid();
            _database.InsertPlayerRecord(playerId, "GuitarHero");

            // Game Record 1
            var game1 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 100000, StarAmount.Star3);
            _database.InsertBandRecord(game1);

            var solo1 = new PlayerScoreRecord
            {
                GameRecordId = game1.Id,
                PlayerId = playerId,
                Instrument = Instrument.FiveFretGuitar,
                Difficulty = Difficulty.Expert,
                Score = 100000,
                Stars = StarAmount.Star3,
                Percent = 0.85f,
                NotesHit = 85,
                NotesMissed = 15
            };
            _database.InsertSoloRecords(new[] { solo1 });

            // Game Record 2 (Higher score)
            var game2 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 220000, StarAmount.StarGold);
            _database.InsertBandRecord(game2);

            var solo2 = new PlayerScoreRecord
            {
                GameRecordId = game2.Id,
                PlayerId = playerId,
                Instrument = Instrument.FiveFretGuitar,
                Difficulty = Difficulty.Expert,
                Score = 220000,
                Stars = StarAmount.StarGold,
                Percent = 0.99f,
                NotesHit = 99,
                NotesMissed = 1
            };
            _database.InsertSoloRecords(new[] { solo2 });

            var bestSolo = _database.QueryPlayerSongHighScore(
                songChecksum: SongChecksumA,
                playerId: playerId,
                instrument: Instrument.FiveFretGuitar,
                highestDifficultyOnly: false,
                currentDifficultyOnly: true,
                currentDifficulty: Difficulty.Expert);

            bestSolo.ShouldNotBeNull();
            bestSolo.Score.ShouldBe(220000);
            bestSolo.GetPercent().ShouldBe(0.99f, 0.001f);
        }

        [Test]
        public void QueryPlayerSongHighestPercentage_ReturnsRunWithBestAccuracy()
        {
            var playerId = Guid.NewGuid();
            _database.InsertPlayerRecord(playerId, "PrecisionMaster");

            var game1 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 300000, StarAmount.Star5);
            _database.InsertBandRecord(game1);

            // Lower accuracy but higher score
            var solo1 = new PlayerScoreRecord
            {
                GameRecordId = game1.Id,
                PlayerId = playerId,
                Instrument = Instrument.FourLaneDrums,
                Difficulty = Difficulty.Expert,
                Score = 300000,
                Percent = 0.94f,
                NotesHit = 94,
                NotesMissed = 6
            };

            var game2 = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 250000, StarAmount.Star5);
            _database.InsertBandRecord(game2);

            // Higher accuracy (Full Combo / 100%) but lower score
            var solo2 = new PlayerScoreRecord
            {
                GameRecordId = game2.Id,
                PlayerId = playerId,
                Instrument = Instrument.FourLaneDrums,
                Difficulty = Difficulty.Expert,
                Score = 250000,
                Percent = 1.0f,
                NotesHit = 100,
                NotesMissed = 0,
                IsFc = true
            };

            _database.InsertSoloRecords(new[] { solo1, solo2 });

            var bestPercentage = _database.QueryPlayerSongHighestPercentage(
                songChecksum: SongChecksumA,
                playerId: playerId,
                instrument: Instrument.FourLaneDrums,
                highestDifficultyOnly: false,
                currentDifficultyOnly: true,
                currentDifficulty: Difficulty.Expert);

            bestPercentage.ShouldNotBeNull();
            bestPercentage.GetPercent().ShouldBe(1.0f, 0.001f);
            bestPercentage.IsFc.ShouldBeTrue();
        }

        [Test]
        public void QueryPlayerScores_SeparatesDifferentPlayers()
        {
            var player1 = Guid.NewGuid();
            var player2 = Guid.NewGuid();

            _database.InsertPlayerRecord(player1, "PlayerOne");
            _database.InsertPlayerRecord(player2, "PlayerTwo");

            var game = CreateGameRecord(SongChecksumA, "Song A", "Artist A", 500000, StarAmount.StarGold);
            _database.InsertBandRecord(game);

            var soloP1 = new PlayerScoreRecord
            {
                GameRecordId = game.Id,
                PlayerId = player1,
                Instrument = Instrument.FiveFretBass,
                Difficulty = Difficulty.Expert,
                Score = 200000
            };

            var soloP2 = new PlayerScoreRecord
            {
                GameRecordId = game.Id,
                PlayerId = player2,
                Instrument = Instrument.Vocals,
                Difficulty = Difficulty.Expert,
                Score = 300000
            };

            _database.InsertSoloRecords(new[] { soloP1, soloP2 });

            var p1Scores = _database.QueryPlayerScores(player1);
            var p2Scores = _database.QueryPlayerScores(player2);

            p1Scores.Count.ShouldBe(1);
            p1Scores[0].Score.ShouldBe(200000);

            p2Scores.Count.ShouldBe(1);
            p2Scores[0].Score.ShouldBe(300000);
        }

        private static GameRecord CreateGameRecord(
            HashWrapper checksum,
            string songName,
            string artist,
            int score,
            StarAmount stars)
        {
            return new GameRecord
            {
                SongChecksum = checksum.HashBytes,
                SongName = songName,
                SongArtist = artist,
                SongCharter = "Charter",
                BandScore = score,
                BandStars = stars,
                Date = DateTime.UtcNow,
                GameVersion = "0.1.0"
            };
        }
    }
}
