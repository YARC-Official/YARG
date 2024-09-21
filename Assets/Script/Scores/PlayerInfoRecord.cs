using System;
using SQLite;

namespace YARG.Scores
{
    [Table("Players")]
    public class PlayerInfoRecord
    {
        // DO NOT change any of these field names
        // without changing the SQL queries!

        [PrimaryKey]
        public Guid Id { get; set; }
        public string Name { get; set; }
    }
}