using System;
using SQLite;
using YARG.Core;
using YARG.Core.Game;

namespace YARG.Scores
{
    [Table("CareerSongCompletionScores")]
    public class CareerSongCompletionScores
    {
        [Indexed][PrimaryKey]
        public int Id { get; set; }
        [Indexed]
        public int CareerSongCompletionId { get; set; }
        [Indexed]
        public int CareerSaveId { get; set; }

        [Indexed]
        public Guid ProfileId { get; set; }
        [Indexed]
        public int PlayerScoreRecordId { get; set; }

        public Instrument Instrument { get; set; }
        public Difficulty Difficulty { get; set; }
        public StarAmount Stars { get; set; }
        public int Score { get; set; }
        public float Percent { get; set; }
        public bool IsFc { get; set; }
    }
}