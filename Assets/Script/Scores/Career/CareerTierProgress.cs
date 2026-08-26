using System;
using SQLite;

namespace YARG.Scores
{
    [Table("CareerTierProgress")]
    public class CareerTierProgress
    {
        [PrimaryKey][AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int CareerSaveId { get; set; }

        [Indexed]
        public Guid TierId { get; set; }

        public DateTime? UnlockedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // TODO: These should be changed to actually query for relevant CareerSongCompletion records
        public bool IsUnlocked => UnlockedAt.HasValue;
        public bool IsCompleted => CompletedAt.HasValue;

        public bool CompletionBonusSeen { get; set; }
    }
}