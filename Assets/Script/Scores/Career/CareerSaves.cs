using System;
using SQLite;

namespace YARG.Scores
{
    // One instance of a career run.
    // There may be multiple runs, either by different profiles or the same profile playing the career again.
    [Table("CareerSaves")]
    public class CareerSaves
    {
        [PrimaryKey][AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public Guid CareerId { get; set; }

        public int CareerVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastPlayedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        // "New career" is a soft delete: progress history is preserved for comparison, the save just stops being active.
        public bool IsDeleted { get; set; }

        public bool IsCompleted => CompletedAt.HasValue;
    }
}