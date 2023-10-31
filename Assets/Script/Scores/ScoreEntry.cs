using System;

namespace YARG.Scores
{
    public class ScoreEntry
    {
        public string SongChecksum { get; set; }
        public DateTime Date { get; set; }

        public string ReplayFileName { get; set; }
        public string ReplayChecksum { get; set; }

        public ScoreInfo[] PlayerScores { get; set; }
        public int         BandScore    { get; set; }
        public StarAmount  BandStars    { get; set; }
    }
}