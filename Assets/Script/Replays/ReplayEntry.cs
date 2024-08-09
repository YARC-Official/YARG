using System;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Game;
using YARG.Core.Replays;
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

        public ReplayReadResult LoadReplay(out Replay replay)
        {
            return ReplayIO.ReadReplay(ReplayPath, out replay);
        }

        public static ReplayEntry CreateFromReplay(Replay replay)
        {
            var entry = new ReplayEntry
            {
                SongName = replay.Metadata.SongName,
                ArtistName = replay.Metadata.ArtistName,
                CharterName = replay.Metadata.CharterName,
                BandScore = replay.Metadata.BandScore,
                BandStars = replay.Metadata.BandStars,
                Date = replay.Metadata.Date,
                SongChecksum = replay.Metadata.SongChecksum,
                PlayerCount = replay.PlayerCount,
                PlayerNames = replay.PlayerNames,
                EngineVersion = replay.Header.EngineVersion
            };

            entry.ReplayPath = Path.Combine(ReplayContainer.ReplayDirectory, entry.GetReplayName());

            return entry;
        }
    }
}