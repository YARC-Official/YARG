using System;
using LiteDB;

namespace YARG.Scores
{
    public class ScoreEntry
    {
        [BsonId]
        public string SongChecksum { get; set; }

        public DateTime LastPlayed { get; set; }
        public DateTime FirstPlayed { get; set; }
        public int TimesPlayed { get; set; }

        [BsonIgnore]
        public string ReplayFileName => $"{SongChecksum}.replay";
    }
}