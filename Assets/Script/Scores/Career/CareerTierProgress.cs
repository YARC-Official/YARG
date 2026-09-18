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

        public int TierIndex { get; set; }

        public DateTime? UnlockedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public bool IsUnlocked => UnlockedAt.HasValue;
        public bool IsCompleted => CompletedAt.HasValue;

        // UI state: has the player seen the unlock for this tier (e.g. a "new tier unlocked" presentation)
        public bool UnlockSeen { get; set; }
        public bool CompletionBonusSeen { get; set; }
    }
}