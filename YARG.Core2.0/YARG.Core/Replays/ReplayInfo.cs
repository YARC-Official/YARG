using System;
using System.IO;
using System.Text.RegularExpressions;
using YARG.Core.Extensions;
using YARG.Core.Game;
using YARG.Core.Song;
using YARG.Core.Utility;

namespace YARG.Core.Replays
{
    public class ReplayInfo
    {
        public readonly string FilePath;
        public readonly string ReplayName;

        public readonly int ReplayVersion;
        public readonly int EngineVersion;
        public readonly HashWrapper ReplayChecksum;

        public readonly string SongName;
        public readonly string ArtistName;
        public readonly string CharterName;
        public readonly float SongSpeed;
        public readonly int BandScore;
        public readonly StarAmount BandStars;
        public readonly double ReplayLength;
        public readonly DateTime Date;
        public readonly HashWrapper SongChecksum;

        public readonly ReplayStats[] Stats;

        public ReplayInfo(string path, string replayName, int replayVersion, int engineVerion, in HashWrapper replayChecksum, string song, string artist, string charter, in HashWrapper songChecksum, in DateTime date, float speed, double length, int score, StarAmount stars, ReplayStats[] stats)
        {
            FilePath = path;
            ReplayName = replayName;

            ReplayVersion = replayVersion;
            EngineVersion = engineVerion;
            ReplayChecksum = replayChecksum;

            SongName = song;
            ArtistName = artist;
            CharterName = charter;
            SongChecksum = songChecksum;
            Date = date;
            SongSpeed = speed;
            ReplayLength = length;
            BandScore = score;
            BandStars = stars;
            Stats = stats;
        }

        public ReplayInfo(string path, UnmanagedMemoryStream stream)
        {
            FilePath = path;

            ReplayVersion = stream.Read<int>(Endianness.Little);
            EngineVersion = stream.Read<int>(Endianness.Little);
            ReplayChecksum = HashWrapper.Deserialize(stream);

            SongName = stream.ReadString();
            ArtistName = stream.ReadString();
            CharterName = stream.ReadString();
            SongChecksum = HashWrapper.Deserialize(stream);
            Date = DateTime.FromBinary(stream.Read<long>(Endianness.Little));
            SongSpeed = stream.Read<float>(Endianness.Little);
            ReplayLength = stream.Read<double>(Endianness.Little);
            BandScore = stream.Read<int>(Endianness.Little);
            BandStars = (StarAmount) stream.ReadByte();

            ReplayName = ConstructReplayName(SongName, ArtistName, CharterName, in Date);

            int statCount = stream.Read<int>(Endianness.Little);
            Stats = new ReplayStats[statCount];
            for (int i = 0; i < statCount; i++)
            {
                var mode = (GameMode) stream.ReadByte();
                Stats[i] = mode switch
                {
                    GameMode.FiveFretGuitar or
                    GameMode.SixFretGuitar => new GuitarReplayStats(stream, ReplayVersion),
                    GameMode.FourLaneDrums or
                    GameMode.FiveLaneDrums => new DrumsReplayStats(stream, ReplayVersion),
                    GameMode.ProKeys       => new ProKeysReplayStats(stream, ReplayVersion),
                    GameMode.Vocals        => new VocalsReplayStats(stream, ReplayVersion),
                    _ => throw new Exception($"Stats for {mode} not supported"),
                };
            }
        }

        public void Serialize(BinaryWriter writer)
        {
            writer.Write(ReplayVersion);
            writer.Write(EngineVersion);
            ReplayChecksum.Serialize(writer);

            writer.Write(SongName);
            writer.Write(ArtistName);
            writer.Write(CharterName);
            SongChecksum.Serialize(writer);
            writer.Write(Date.ToBinary());
            writer.Write(SongSpeed);
            writer.Write(ReplayLength);
            writer.Write(BandScore);
            writer.Write((byte) BandStars);

            writer.Write(Stats.Length);
            foreach (var stat in Stats)
            {
                stat.Serialize(writer);
            }
        }

        // Remove invalid characters from the replay name
        private static readonly Regex ReplayNameRegex = new("[<>:\"/\\|?*]", RegexOptions.Compiled);
        public static string ConstructReplayName(string song, string artist, string charter, in DateTime date)
        {
            var strippedSong = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(song), "");
            var strippedArtist = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(artist), "");
            var strippedCharter = ReplayNameRegex.Replace(RichTextUtils.StripRichTextTags(charter), "");

            return $"{strippedArtist}-{strippedSong}-{strippedCharter}-{date:yy-MM-dd-HH-mm-ss}";
        }
    }
}
