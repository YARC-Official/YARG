using System;
using SQLite;
using YARG.Core.Game;

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
        public DateTime Date { get; set; }

        [Indexed]
        public byte[] SongChecksum { get; set; }

        public string GameVersion { get; set; }

        // Keep this information in case the user doesn't have the song
        public string SongName    { get; set; }
        public string SongArtist  { get; set; }
        public string SongCharter { get; set; }

        public string ReplayFileName { get; set; }
        public byte[] ReplayChecksum { get; set; }

        public int        BandScore { get; set; }
        public StarAmount BandStars { get; set; }

        public float SongSpeed        { get; set; }
        public bool  PlayedWithReplay { get; set; }
    }
}