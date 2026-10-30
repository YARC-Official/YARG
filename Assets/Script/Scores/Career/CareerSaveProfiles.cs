using System;
using SQLite;

namespace YARG.Scores
{
    // The profile or profiles associated with a particular career save.
    [Table("CareerSaveProfiles")]
    public class CareerSaveProfiles
    {
        [PrimaryKey][AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public int CareerSaveId { get; set; }

        [Indexed]
        public Guid ProfileId { get; set; }
    }
}