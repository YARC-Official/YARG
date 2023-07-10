using EasySharpIni.Converters;
using System.Collections.Generic;
using System.IO;
using EasySharpIni;
using YARG.Audio;

namespace YARG.Song
{
    public class IniSongEntry : SongEntry
    {
        private static readonly IntConverter IntConverter = new();
        private static readonly BooleanConverter BooleanConverter = new();

        public string Playlist { get; private set; } = string.Empty;
        public string SubPlaylist { get; private set; } = string.Empty;

        public bool IsModChart { get; private set; }
        public bool HasLyrics { get; private set; }

        public int VideoStartOffset { get; private set; }

        public IniSongEntry(BinaryReader reader, string folder) : base(reader, folder)
        {
            Playlist = reader.ReadString();
            SubPlaylist = reader.ReadString();
            IsModChart = reader.ReadBoolean();
            HasLyrics = reader.ReadBoolean();
            VideoStartOffset = reader.ReadInt32();
        }

        public override void WriteMetadataToCache(BinaryWriter writer)
        {
            writer.Write((int) SongType.SongIni);
            base.WriteMetadataToCache(writer);
            writer.Write(Playlist);
            writer.Write(SubPlaylist);
            writer.Write(IsModChart);
            writer.Write(HasLyrics);
            writer.Write(VideoStartOffset);
        }

        public IniSongEntry(string cache, string directory, string checksum, string notesFile, ulong tracks)
        {
            CacheRoot = cache;
            Location = directory;
            Checksum = checksum;
            NotesFile = notesFile;
            AvailableParts = tracks;
        }

        public ScanResult ParseIni()
        {
            // We have a song.ini, notes file and audio. The song is scannable.
            var file = new IniFile(Path.Combine(Location, "song.ini"));

            // Had some reports that ini parsing might throw an exception, leaving this in for now
            // in as I don't know the cause just yet and I want to investigate it further.
            file.Parse();

            string sectionName = file.ContainsSection("song") ? "song" : "Song";

            if (!file.ContainsSection(sectionName)) return ScanResult.NotASong;

            var section = file.GetSection(sectionName);

            Name = section.GetField("name");
            Artist = section.GetField("artist");
            Charter = section.GetField("charter");
            IsMaster = true; // just gonna assume every ini song is the original artist

            Album = section.GetField("album");
            AlbumTrack = section.GetField("album_track", "0").Get(IntConverter);
            if (section.ContainsField("track"))
            {
                AlbumTrack = section.GetField("track", "0").Get(IntConverter);
            }

            PlaylistTrack = section.GetField("playlist_track", "0").Get(IntConverter);

            Genre = section.GetField("genre");
            Year = section.GetField("year");

            SongLength = section.GetField("song_length", "-1").Get(IntConverter);
            PreviewStart = section.GetField("preview_start_time", "0").Get(IntConverter);
            PreviewEnd = section.GetField("preview_end_time", "-1").Get(IntConverter);

            {
                var delayField = section.GetField("delay");
                string delay = delayField;
                // Decimal format (seconds)
                if (delay.Contains("."))
                {
                    Delay = double.Parse(delay);
                }
                else
                {
                    int rawDelay = delayField.Get(IntConverter);
                    Delay = rawDelay / 1000.0;
                }
            }

            HopoThreshold = section.GetField("hopo_frequency", "170").Get(IntConverter);
            EighthNoteHopo = section.GetField("eighthnote_hopo", "false").Get(BooleanConverter);
            MultiplierNote = section.GetField("multiplier_note", "116").Get(IntConverter);

            PartDifficulties = new()
            {
                {
                    Data.Instrument.GUITAR, section.GetField("diff_guitar", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.GUITAR_COOP, section.GetField("diff_guitar_coop", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.REAL_GUITAR, section.GetField("diff_guitar_real", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.RHYTHM, section.GetField("diff_rhythm", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.BASS, section.GetField("diff_bass", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.REAL_BASS, section.GetField("diff_bass_real", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.DRUMS, section.GetField("diff_drums", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.GH_DRUMS, section.GetField("diff_drums", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.REAL_DRUMS, section.GetField("diff_drums_real", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.KEYS, section.GetField("diff_keys", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.REAL_KEYS, section.GetField("diff_keys_real", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.VOCALS, section.GetField("diff_vocals", "-1").Get(IntConverter)
                },
                {
                    Data.Instrument.HARMONY, section.GetField("diff_vocals_harm", "-1").Get(IntConverter)
                },
            };

            BandDifficulty = section.GetField("diff_band", "-1").Get(IntConverter);

            // TODO: Preparse this
            // Get vocal count from difficulties
            if (PartDifficulties.GetValueOrDefault(Data.Instrument.HARMONY, -1) != -1)
            {
                VocalParts = 3;
            }
            else if (PartDifficulties.GetValueOrDefault(Data.Instrument.VOCALS, -1) != -1)
            {
                VocalParts = 1;
            }
            else
            {
                VocalParts = 0;
            }

            DrumType = DrumType.Unknown;
            if (section.GetField("pro_drums", "false").Get(BooleanConverter))
            {
                DrumType = DrumType.FourLane;
            }
            else if (section.GetField("five_lane_drums", "false").Get(BooleanConverter))
            {
                DrumType = DrumType.FiveLane;
            }

            LoadingPhrase = section.GetField("loading_phrase");
            Source = section.GetField("icon");
            HasLyrics = section.GetField("lyrics").Get(BooleanConverter);
            IsModChart = section.GetField("modchart").Get(BooleanConverter);
            VideoStartOffset = section.GetField("video_start_time", "0").Get(IntConverter);
            return ScanResult.Ok;
        }

        public override void LoadAudio(IAudioManager manager, float speed, params SongStem[] ignoreStems)
        {
            var stems = AudioHelpers.GetSupportedStems(Location);
            foreach (var stem in ignoreStems)
                stems.Remove(stem);
            manager.LoadSong(stems, speed);
        }
    }
}