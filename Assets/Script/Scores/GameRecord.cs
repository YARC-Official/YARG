using System;
using SQLite;

namespace YARG.Scores
{
    [Table("GameRecords")]
    public class GameRecord
    {
        // DO NOT change any of these field names
        // without changing the SQL queries!

        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }

        [Indexed]
        public string   SongChecksum { get; set; }
        public DateTime Date         { get; set; }

        public string ReplayFileName { get; set; }
        public string ReplayChecksum { get; set; }

        public int        BandScore { get; set; }
        public StarAmount BandStars { get; set; }
    }
}