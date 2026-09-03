using NUnit.Framework;
using Shouldly;
using YARG.Scores;

namespace YARG.UnitTests
{
    public sealed class PlayerScoreRecordTests
    {
        [Test]
        public void PlayerScoreRecord_GetPercent_WhenPercentIsExplicit_ReturnsExplicitValue()
        {
            var record = new PlayerScoreRecord
            {
                Percent = 0.985f,
                NotesHit = 50,
                NotesMissed = 50
            };

            record.GetPercent().ShouldBe(0.985f, 0.0001f);
        }

        [Test]
        public void PlayerScoreRecord_GetPercent_WhenPercentIsNull_CalculatesFromNotes()
        {
            var perfect = new PlayerScoreRecord
            {
                Percent = null,
                NotesHit = 100,
                NotesMissed = 0
            };
            perfect.GetPercent().ShouldBe(1.0f, 0.0001f);

            var partial = new PlayerScoreRecord
            {
                Percent = null,
                NotesHit = 75,
                NotesMissed = 25
            };
            partial.GetPercent().ShouldBe(0.75f, 0.0001f);

            var allMissed = new PlayerScoreRecord
            {
                Percent = null,
                NotesHit = 0,
                NotesMissed = 100
            };
            allMissed.GetPercent().ShouldBe(0.0f, 0.0001f);
        }

        [Test]
        public void PlayerScoreRecord_GetPercent_WhenNoNotes_ReturnsZero()
        {
            var record = new PlayerScoreRecord
            {
                Percent = null,
                NotesHit = 0,
                NotesMissed = 0
            };
            record.GetPercent().ShouldBe(0.0f, 0.0001f);
        }
    }
}
