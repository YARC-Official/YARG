using System;
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

        [Indexed]
        public Guid   PlayerId   { get; set; }

        [Indexed]
        public Instrument Instrument { get; set; }
        public Difficulty Difficulty { get; set; }

        public int        Score { get; set; }
        public StarAmount Stars { get; set; }

        public int  NotesHit    { get; set; }
        public int  NotesMissed { get; set; }
        public bool IsFc        { get; set; }

        [Ignore]
        public float Percent => (float) NotesHit / (NotesHit + NotesMissed);
    }
}