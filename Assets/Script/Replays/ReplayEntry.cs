using System;
using System.Text.RegularExpressions;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Replays
{
    public class ReplayEntry
    {
        // Remove invalid characters from the replay name
        private static readonly Regex ReplayNameRegex = new("[<>:\"/\\|?*]", RegexOptions.Compiled);

        public string      SongName;
        public string      ArtistName;
        public string      CharterName;
        public int         BandScore;
        public StarAmount  BandStars;
        public DateTime    Date;
        public HashWrapper SongChecksum;
        public int         PlayerCount;
        public string[]    PlayerNames;

        public int EngineVersion;

        public string ReplayPath;

        public string GetReplayName()
        {
            var song = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(SongName), "");
            var artist = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(ArtistName), "");
            var charter = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(CharterName), "");

            return $"{artist}-{song}-{charter}-{Date:yy-MM-dd-HH-mm-ss}.replay";
        }
    }
}