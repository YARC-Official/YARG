using System;

namespace YARG.Replays
{
    public class ReplayEntry
    {
        public string   SongName;
        public string   ArtistName;
        public string   CharterName;
        public int      BandScore;
        public DateTime Date;
        public string   SongChecksum;
        public int      PlayerCount;
        public string[] PlayerNames;

        public int GameVersion;

        public string ReplayFile;
    }
}