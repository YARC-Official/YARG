using System;
using System.Text.RegularExpressions;

namespace YARG.Replays
{
    public class ReplayEntry
    {
        // Remove invalid characters from the replay name
        private static readonly Regex ReplayNameRegex = new("[<>:\"/\\|?*]", RegexOptions.Compiled);

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

        public string GetReplayName()
        {
            var song = ReplayNameRegex.Replace(SongName, "");
            var artist = ReplayNameRegex.Replace(ArtistName, "");
            var charter = ReplayNameRegex.Replace(CharterName, "");

            return $"{artist}-{song}-{charter}-{Date:yy-MM-dd-HH-mm-ss}.replay";
        }
    }
}