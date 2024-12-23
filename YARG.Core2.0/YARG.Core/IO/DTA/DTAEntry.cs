using System;
using System.Text;
using YARG.Core.Song;

namespace YARG.Core.IO
{
    public class DTAEntry
    {
        public string? Name;
        public string? Artist;
        public string? Album;
        public string? Genre;
        public string? Charter;
        public string? Source;
        public string? Playlist;
        public int? YearAsNumber;

        public ulong? SongLength;
        public uint? SongRating;  // 1 = FF; 2 = SR; 3 = M; 4 = NR

        public long? PreviewStart;
        public long? PreviewEnd;
        public bool? IsMaster;

        public int? AlbumTrack;

        public string? SongID;
        public uint? AnimTempo;
        public string? DrumBank;
        public string? VocalPercussionBank;
        public uint? VocalSongScrollSpeed;
        public bool? VocalGender; //true for male, false for female
        //public bool HasAlbumArt;
        //public bool IsFake;
        public uint? VocalTonicNote;
        public bool? SongTonality; // 0 = major, 1 = minor
        public int? TuningOffsetCents;
        public uint? VenueVersion;

        public string[]? Soloes;
        public string[]? VideoVenues;

        public int[]? RealGuitarTuning;
        public int[]? RealBassTuning;

        public RBAudio<int>? Indices;
        public int[]? CrowdChannels;

        public string? Location;
        public float[]? Pans;
        public float[]? Volumes;
        public float[]? Cores;
        public long? HopoThreshold;
        public Encoding Encoding;

        public bool DiscUpdate;

        public RBCONDifficulties Difficulties = RBCONDifficulties.Default;

        public DTAEntry(Encoding encoding)
        {
            Encoding = encoding;
        }

        public DTAEntry(string nodename, in YARGTextContainer<byte> container)
        {
            Encoding = container.Encoding;
            LoadData(nodename, container);
        }

        public void LoadData(string nodename, YARGTextContainer<byte> container)
        {
            container.Encoding = Encoding;
            while (YARGDTAReader.StartNode(ref container))
            {
                string name = YARGDTAReader.GetNameOfNode(ref container, false);
                switch (name)
                {
                    case "name": Name = YARGDTAReader.ExtractText(ref container); break;
                    case "artist": Artist = YARGDTAReader.ExtractText(ref container); break;
                    case "master": IsMaster = YARGDTAReader.ExtractBoolean_FlippedDefault(ref container); break;
                    case "context":
                        unsafe
                        {
                            int scopeLevel = 0;
                            while (container.Position < container.End && *container.Position != ')')
                            {
                                switch (*container.Position++)
                                {
                                    case (byte)'{': ++scopeLevel; break;
                                    case (byte)'}': --scopeLevel; break;
                                }
                            }

                            if (scopeLevel != 0)
                            {
                                throw new Exception("Invalid Context - Unbalanced brace count!");
                            }
                            break;
                        }
                    case "song":
                        while (YARGDTAReader.StartNode(ref container))
                        {
                            string descriptor = YARGDTAReader.GetNameOfNode(ref container, false);
                            switch (descriptor)
                            {
                                case "name": Location = YARGDTAReader.ExtractText(ref container); break;
                                case "tracks":
                                    {
                                        var indices = RBAudio<int>.Empty;
                                        while (YARGDTAReader.StartNode(ref container))
                                        {
                                            while (YARGDTAReader.StartNode(ref container))
                                            {
                                                switch (YARGDTAReader.GetNameOfNode(ref container, false))
                                                {
                                                    case "drum": indices.Drums = YARGDTAReader.ExtractArray_Int(ref container); break;
                                                    case "bass": indices.Bass = YARGDTAReader.ExtractArray_Int(ref container); break;
                                                    case "guitar": indices.Guitar = YARGDTAReader.ExtractArray_Int(ref container); break;
                                                    case "keys": indices.Keys = YARGDTAReader.ExtractArray_Int(ref container); break;
                                                    case "vocals": indices.Vocals = YARGDTAReader.ExtractArray_Int(ref container); break;
                                                }
                                                YARGDTAReader.EndNode(ref container);
                                            }
                                            YARGDTAReader.EndNode(ref container);
                                        }
                                        Indices = indices;
                                        break;
                                    }
                                case "crowd_channels": CrowdChannels = YARGDTAReader.ExtractArray_Int(ref container); break;
                                //case "vocal_parts": VocalParts = YARGDTAReader.ExtractUInt16(ref container); break;
                                case "pans": Pans = YARGDTAReader.ExtractArray_Float(ref container); break;
                                case "vols": Volumes = YARGDTAReader.ExtractArray_Float(ref container); break;
                                case "cores": Cores = YARGDTAReader.ExtractArray_Float(ref container); break;
                                case "hopo_threshold": HopoThreshold = YARGDTAReader.ExtractInt64(ref container); break;
                            }
                            YARGDTAReader.EndNode(ref container);
                        }
                        break;
                    case "song_vocals": while (YARGDTAReader.StartNode(ref container)) YARGDTAReader.EndNode(ref container); break;
                    case "song_scroll_speed": VocalSongScrollSpeed = YARGDTAReader.ExtractUInt32(ref container); break;
                    case "tuning_offset_cents": TuningOffsetCents = YARGDTAReader.ExtractInt32(ref container); break;
                    case "bank": VocalPercussionBank = YARGDTAReader.ExtractText(ref container); break;
                    case "anim_tempo":
                        {
                            string val = YARGDTAReader.ExtractText(ref container);
                            AnimTempo = val switch
                            {
                                "kTempoSlow" => 16,
                                "kTempoMedium" => 32,
                                "kTempoFast" => 64,
                                _ => uint.Parse(val)
                            };
                            break;
                        }
                    case "preview":
                        PreviewStart = YARGDTAReader.ExtractInt64(ref container);
                        PreviewEnd = YARGDTAReader.ExtractInt64(ref container);
                        break;
                    case "rank":
                        while (YARGDTAReader.StartNode(ref container))
                        {
                            string descriptor = YARGDTAReader.GetNameOfNode(ref container, false);
                            int diff = YARGDTAReader.ExtractInt32(ref container);
                            switch (descriptor)
                            {
                                case "drum":
                                case "drums": Difficulties.FourLaneDrums = (short) diff; break;

                                case "guitar": Difficulties.FiveFretGuitar = (short) diff; break;
                                case "bass": Difficulties.FiveFretBass = (short) diff; break;
                                case "vocals": Difficulties.LeadVocals = (short) diff; break;
                                case "keys": Difficulties.Keys = (short) diff; break;

                                case "realGuitar":
                                case "real_guitar": Difficulties.ProGuitar = (short) diff; break;

                                case "realBass":
                                case "real_bass": Difficulties.ProBass = (short) diff; break;

                                case "realKeys":
                                case "real_keys": Difficulties.ProKeys = (short) diff; break;

                                case "realDrums":
                                case "real_drums": Difficulties.ProDrums = (short) diff; break;

                                case "harmVocals":
                                case "vocal_harm": Difficulties.HarmonyVocals = (short) diff; break;

                                case "band": Difficulties.Band = (short) diff; break;
                            }
                            YARGDTAReader.EndNode(ref container);
                        }
                        break;
                    case "solo": Soloes = YARGDTAReader.ExtractArray_String(ref container); break;
                    case "genre": Genre = YARGDTAReader.ExtractText(ref container); break;
                    case "decade": /*Decade = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "vocal_gender": VocalGender = YARGDTAReader.ExtractText(ref container) == "male"; break;
                    case "format": /*Format = YARGDTAReader.ExtractUInt32(ref container);*/ break;
                    case "version": VenueVersion = YARGDTAReader.ExtractUInt32(ref container); break;
                    case "fake": /*IsFake = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "downloaded": /*Downloaded = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "game_origin":
                        {
                            string str = YARGDTAReader.ExtractText(ref container);
                            if ((str == "ugc" || str == "ugc_plus"))
                            {
                                if (!nodename.StartsWith("UGC_"))
                                    Source = "customs";
                            }
                            else if (str == "#ifdef")
                            {
                                string conditional = YARGDTAReader.ExtractText(ref container);
                                if (conditional == "CUSTOMSOURCE")
                                {
                                    Source = YARGDTAReader.ExtractText(ref container);
                                }
                                else
                                {
                                    Source = "customs";
                                }
                            }
                            else
                            {
                                Source = str;
                            }

                            //// if the source is any official RB game or its DLC, charter = Harmonix
                            //if (SongSources.GetSource(str).Type == SongSources.SourceType.RB)
                            //{
                            //    _charter = "Harmonix";
                            //}

                            //// if the source is meant for usage in TBRB, it's a master track
                            //// TODO: NEVER assume localized version contains "Beatles"
                            //if (SongSources.SourceToGameName(str).Contains("Beatles")) _isMaster = true;
                            break;
                        }
                    case "song_id": SongID = YARGDTAReader.ExtractText(ref container); break;
                    case "rating": SongRating = YARGDTAReader.ExtractUInt32(ref container); break;
                    case "short_version": /*ShortVersion = YARGDTAReader.ExtractUInt32(ref container);*/ break;
                    case "album_art": /*HasAlbumArt = YARGDTAReader.ExtractBoolean(ref container);*/ break;
                    case "year_released":
                    case "year_recorded": YearAsNumber = YARGDTAReader.ExtractInt32(ref container); break;
                    case "album_name": Album = YARGDTAReader.ExtractText(ref container); break;
                    case "album_track_number": AlbumTrack = YARGDTAReader.ExtractInt32(ref container); break;
                    case "pack_name": Playlist = YARGDTAReader.ExtractText(ref container); break;
                    case "base_points": /*BasePoints = YARGDTAReader.ExtractUInt32(ref container);*/ break;
                    case "band_fail_cue": /*BandFailCue = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "drum_bank": DrumBank = YARGDTAReader.ExtractText(ref container); break;
                    case "song_length": SongLength = YARGDTAReader.ExtractUInt64(ref container); break;
                    case "sub_genre": /*Subgenre = YARGDTAReader.ExtractText(ref container);*/ break;
                    case "author": Charter = YARGDTAReader.ExtractText(ref container); break;
                    case "guide_pitch_volume": /*GuidePitchVolume = YARGDTAReader.ExtractFloat(ref container);*/ break;
                    case "encoding":
                        Encoding = YARGDTAReader.ExtractText(ref container).ToLower() switch
                        {
                            "latin1" => YARGTextReader.Latin1,
                            "utf-8" or
                            "utf8" => Encoding.UTF8,
                            _ => container.Encoding
                        };

                        var currEncoding = container.Encoding;
                        if (currEncoding != Encoding)
                        {
                            string Convert(string str)
                            {
                                byte[] bytes = currEncoding.GetBytes(str);
                                return Encoding.GetString(bytes);
                            }

                            if (Name != null)
                                Name = Convert(Name);
                            if (Artist != null)
                                Artist = Convert(Artist);
                            if (Album != null)
                                Album = Convert(Album);
                            if (Genre != null)
                                Genre = Convert(Genre);
                            if (Charter != null)
                                Charter = Convert(Charter);
                            if (Source != null)
                                Source = Convert(Source);

                            if (Playlist != null)
                                Playlist = Convert(Playlist);
                            container.Encoding = Encoding;
                        }

                        break;
                    case "vocal_tonic_note": VocalTonicNote = YARGDTAReader.ExtractUInt32(ref container); break;
                    case "song_tonality": SongTonality = YARGDTAReader.ExtractBoolean(ref container); break;
                    case "alternate_path": /*AlternatePath = YARGDTAReader.ExtractBoolean(ref container);*/ break;
                    case "real_guitar_tuning": RealGuitarTuning = YARGDTAReader.ExtractArray_Int(ref container); break;
                    case "real_bass_tuning": RealBassTuning = YARGDTAReader.ExtractArray_Int(ref container); break;
                    case "video_venues": VideoVenues = YARGDTAReader.ExtractArray_String(ref container); break;
                    case "extra_authoring":
                        {
                            StringBuilder authors = new();
                            foreach (string str in YARGDTAReader.ExtractArray_String(ref container))
                            {
                                if (str == "disc_update")
                                {
                                    DiscUpdate = true;
                                }
                                else
                                {
                                    if (authors.Length == 0 && Charter == SongMetadata.DEFAULT_CHARTER)
                                    {
                                        authors.Append(str);
                                    }
                                    else
                                    {
                                        if (authors.Length == 0)
                                            authors.Append(Charter);
                                        authors.Append(", " + str);
                                    }
                                }
                            }

                            if (authors.Length == 0)
                            {
                                authors.Append(Charter);
                            }

                            Charter = authors.ToString();
                        }
                        break;
                }
                YARGDTAReader.EndNode(ref container);
            }
        }
    }
}
