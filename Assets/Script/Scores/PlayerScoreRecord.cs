using SQLite;
using YARG.Core;

namespace YARG.Scores
{
    [Table("PlayerScores")]
    public class PlayerScoreRecord
    {
        // DO NOT change any of these field names
        // without changing the SQL queries!

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int    GameRecordId { get; set; }
        [Indexed]
        public string SongChecksum { get; set; }

        public string PlayerName { get; set; }

        public Instrument Instrument { get; set; }
        public Difficulty Difficulty { get; set; }

        public int        Score { get; set; }
        public StarAmount Stars { get; set; }

        public float Percent { get; set; }
        public bool  IsFc    { get; set; }
    }
}