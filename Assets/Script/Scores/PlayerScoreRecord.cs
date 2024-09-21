using System;
using SQLite;
using YARG.Core;
using YARG.Core.Game;

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
        public int GameRecordId { get; set; }

        [Indexed]
        public Guid PlayerId { get; set; }

        [Indexed]
        public Instrument Instrument { get; set; }
        public Difficulty Difficulty { get; set; }

        public Guid EnginePresetId { get; set; }

        public int        Score { get; set; }
        public StarAmount Stars { get; set; }

        public int  NotesHit    { get; set; }
        public int  NotesMissed { get; set; }
        public bool IsFc        { get; set; }

        /// <remarks>
        /// This property was added afterwards, so it is nullable.
        /// Use <see cref="GetPercent"/> to get the actual percent value.
        /// </remarks>
        public float? Percent { get; set; }

        public float GetPercent()
        {
            return Percent
                ?? (float) NotesHit / (NotesHit + NotesMissed);
        }
    }
}