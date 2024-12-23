using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song;

namespace YARG.Core.IO.Ini
{
    public static class SongIniHandler
    {
        public static IniSection ReadSongIniFile(string iniPath)
        {
            var modifiers = YARGIniReader.ReadIniFile(iniPath, SONG_INI_DICTIONARY);
            if (!modifiers.TryGetValue("[song]", out var section))
            {
                section = new IniSection();
            }
            return section;
        }

        public static readonly Dictionary<string, Dictionary<string, IniModifierCreator>> SONG_INI_DICTIONARY;
        public static readonly Dictionary<string, IniModifierCreator> SONG_INI_MODIFIERS;

#if DEBUG
        private static readonly Dictionary<string, ModifierType> _types;
        private static readonly Dictionary<string, ModifierType> _outputs;
        public static void ThrowIfNot<T>(string key)
        {
            if (!_outputs.TryGetValue(key, out var modifierType))
            {
                throw new ArgumentException($"Dev: {key} is not a valid modifier!");
            }

            var typename = typeof(T).Name;
            var type = _types[typename];
            if (type != modifierType
            && (type == ModifierType.SortString) != (modifierType == ModifierType.SortString_Chart)
            && (type == ModifierType.String) != (modifierType == ModifierType.String_Chart))
            {
                throw new ArgumentException($"Dev: Modifier {key} is not of type {typename}");
            }
        }
#endif

        static SongIniHandler()
        {
            SONG_INI_MODIFIERS = new()
            {
                { "album",                                new("album", ModifierType.SortString) },
                { "album_track",                          new("album_track", ModifierType.Int32) },
                { "artist",                               new("artist", ModifierType.SortString) },

                { "background",                           new("background", ModifierType.String) },
                //{ "banner_link_a",                        new("banner_link_a", ModifierType.String) },
                //{ "banner_link_b",                        new("banner_link_b", ModifierType.String) },
                { "bass_type",                            new("bass_type", ModifierType.UInt32) },
                //{ "boss_battle",                          new("boss_battle", ModifierType.Bool) },

                //{ "cassettecolor",                        new("cassettecolor", ModifierType.UInt32) },
                { "charter",                              new("charter", ModifierType.SortString) },
                { "count",                                new("count", ModifierType.UInt32) },
                { "cover",                                new("cover", ModifierType.String) },
                { "credit_written_by",                    new("credit_written_by", ModifierType.String) },
                { "credit_performed_by",                  new("credit_performed_by", ModifierType.String) },
                { "credit_courtesy_of",                   new("credit_courtesy_of", ModifierType.String) },
                { "credit_album_cover",                   new("credit_album_cover", ModifierType.String) },
                { "credit_license",                       new("credit_license", ModifierType.String) },

                { "dance_type",                           new("dance_type", ModifierType.UInt32) },
                { "delay",                                new("delay", ModifierType.Int64) },
                { "diff_band",                            new("diff_band", ModifierType.Int32) },
                { "diff_bass",                            new("diff_bass", ModifierType.Int32) },
                { "diff_bass_real",                       new("diff_bass_real", ModifierType.Int32) },
                { "diff_bass_real_22",                    new("diff_bass_real_22", ModifierType.Int32) },
                { "diff_bassghl",                         new("diff_bassghl", ModifierType.Int32) },
                { "diff_dance",                           new("diff_dance", ModifierType.Int32) },
                { "diff_drums",                           new("diff_drums", ModifierType.Int32) },
                { "diff_drums_real",                      new("diff_drums_real", ModifierType.Int32) },
                { "diff_drums_real_ps",                   new("diff_drums_real_ps", ModifierType.Int32) },
                { "diff_guitar",                          new("diff_guitar", ModifierType.Int32) },
                { "diff_guitar_coop",                     new("diff_guitar_coop", ModifierType.Int32) },
                { "diff_guitar_coop_ghl",                 new("diff_guitar_coop_ghl", ModifierType.Int32) },
                { "diff_guitar_real",                     new("diff_guitar_real", ModifierType.Int32) },
                { "diff_guitar_real_22",                  new("diff_guitar_real_22", ModifierType.Int32) },
                { "diff_guitarghl",                       new("diff_guitarghl", ModifierType.Int32) },
                { "diff_keys",                            new("diff_keys", ModifierType.Int32) },
                { "diff_keys_real",                       new("diff_keys_real", ModifierType.Int32) },
                { "diff_keys_real_ps",                    new("diff_keys_real_ps", ModifierType.Int32) },
                { "diff_rhythm",                          new("diff_rhythm", ModifierType.Int32) },
                { "diff_rhythm_ghl",                      new("diff_rhythm_ghl", ModifierType.Int32) },
                { "diff_vocals",                          new("diff_vocals", ModifierType.Int32) },
                { "diff_vocals_harm",                     new("diff_vocals_harm", ModifierType.Int32) },
                { "drum_fallback_blue",                   new("drum_fallback_blue", ModifierType.Bool) },

                //{ "early_hit_window_size",                new("early_hit_window_size", ModifierType.String) },
                { "eighthnote_hopo",                      new("eighthnote_hopo", ModifierType.Bool) },
                { "end_events",                           new("end_events", ModifierType.Bool) },
                //{ "eof_midi_import_drum_accent_velocity", new("eof_midi_import_drum_accent_velocity", ModifierType.UInt16) },
                //{ "eof_midi_import_drum_ghost_velocity",  new("eof_midi_import_drum_ghost_velocity", ModifierType.UInt16) },

                { "five_lane_drums",                      new("five_lane_drums", ModifierType.Bool) },
                { "frets",                                new("frets", ModifierType.SortString) },

                { "genre",                                new("genre", ModifierType.SortString) },
                { "guitar_type",                          new("guitar_type", ModifierType.UInt32) },

                { "hopo_frequency",                       new("hopo_frequency", ModifierType.Int64) },
                { "hopofreq",                             new("hopofreq", ModifierType.Int32) },

                { "icon",                                 new("icon", ModifierType.SortString) },

                { "keys_type",                            new("keys_type", ModifierType.UInt32) },
                { "kit_type",                             new("kit_type", ModifierType.UInt32) },

                //{ "link_name_a",                          new("link_name_a", ModifierType.String) },
                //{ "link_name_b",                          new("link_name_b", ModifierType.String) },
                { "loading_phrase",                       new("loading_phrase", ModifierType.String) },
                { "lyrics",                               new("lyrics", ModifierType.Bool) },

                { "modchart",                             new("modchart", ModifierType.Bool) },
                { "multiplier_note",                      new("multiplier_note", ModifierType.Int32) },

                { "name",                                 new("name", ModifierType.SortString) },

                { "playlist",                             new("playlist", ModifierType.SortString) },
                { "playlist_track",                       new("playlist_track", ModifierType.Int32) },
                { "preview",                              new("preview", ModifierType.Int64Array) },
                { "preview_end_time",                     new("preview_end_time", ModifierType.Int64) },
                { "preview_start_time",                   new("preview_start_time", ModifierType.Int64) },

                { "pro_drum",                             new("pro_drums", ModifierType.Bool) },
                { "pro_drums",                            new("pro_drums", ModifierType.Bool) },

                { "rating",                               new("rating", ModifierType.UInt32) },
                { "real_bass_22_tuning",                  new("real_bass_22_tuning", ModifierType.UInt32) },
                { "real_bass_tuning",                     new("real_bass_tuning", ModifierType.UInt32) },
                { "real_guitar_22_tuning",                new("real_guitar_22_tuning", ModifierType.UInt32) },
                { "real_guitar_tuning",                   new("real_guitar_tuning", ModifierType.UInt32) },
                { "real_keys_lane_count_left",            new("real_keys_lane_count_left", ModifierType.UInt32) },
                { "real_keys_lane_count_right",           new("real_keys_lane_count_right", ModifierType.UInt32) },

                //{ "scores",                               new("scores", ModifierType.String) },
                //{ "scores_ext",                           new("scores_ext", ModifierType.String) },
                { "song_length",                          new("song_length", ModifierType.UInt64) },
                { "star_power_note",                      new("multiplier_note", ModifierType.Int32) },
                { "sub_genre",                            new("sub_genre", ModifierType.SortString) },
                { "sub_playlist",                         new("sub_playlist", ModifierType.SortString) },
                { "sustain_cutoff_threshold",             new("sustain_cutoff_threshold", ModifierType.Int64) },
                //{ "sysex_high_hat_ctrl",                  new("sysex_high_hat_ctrl", ModifierType.Bool) },
                //{ "sysex_open_bass",                      new("sysex_open_bass", ModifierType.Bool) },
                //{ "sysex_pro_slide",                      new("sysex_pro_slide", ModifierType.Bool) },
                //{ "sysex_rimshot",                        new("sysex_rimshot", ModifierType.Bool) },
                //{ "sysex_slider",                         new("sysex_slider", ModifierType.Bool) },

                { "tags",                                 new("tags", ModifierType.String) },
                { "track",                                new("album_track", ModifierType.Int32) },
                { "tutorial",                             new("tutorial", ModifierType.Bool) },

                { "unlock_completed",                     new("unlock_completed", ModifierType.UInt32) },
                { "unlock_id",                            new("unlock_id", ModifierType.String) },
                { "unlock_require",                       new("unlock_require", ModifierType.String) },
                { "unlock_text",                          new("unlock_text", ModifierType.String) },

                { "version",                              new("version", ModifierType.UInt32) },
                { "video",                                new("video", ModifierType.String) },
                { "video_end_time",                       new("video_end_time", ModifierType.Int64) },
                { "video_loop",                           new("video_loop", ModifierType.Bool) },
                { "video_start_time",                     new("video_start_time", ModifierType.Int64) },
                { "vocal_gender",                         new("vocal_gender", ModifierType.UInt32) },

                { "year",                                 new("year", ModifierType.String) },
            };

            SONG_INI_DICTIONARY = new()
            {
                { "[song]", SONG_INI_MODIFIERS }
            };

#if DEBUG
            _types = new()
            {
                { nameof(SortString), ModifierType.SortString },
                { typeof(string).Name, ModifierType.String },
                { typeof(ulong).Name, ModifierType.UInt64 },
                { typeof(long).Name, ModifierType.Int64 },
                { typeof(uint).Name, ModifierType.UInt32 },
                { typeof(int).Name, ModifierType.Int32 },
                { typeof(ushort).Name, ModifierType.UInt16 },
                { typeof(short).Name, ModifierType.Int16 },
                { typeof(bool).Name, ModifierType.Bool },
                { typeof(float).Name, ModifierType.Float },
                { typeof(double).Name, ModifierType.Double },
                { typeof(long[]).Name, ModifierType.Int64Array },
            };

            _outputs = new Dictionary<string, ModifierType>();
            _outputs.EnsureCapacity(SONG_INI_MODIFIERS.Count + YARGChartFileReader.CHART_MODIFIERS.Count);
            foreach (var node in SONG_INI_MODIFIERS.Values)
            {
                _outputs.TryAdd(node.OutputName, node.Type);
            }

            foreach (var node in YARGChartFileReader.CHART_MODIFIERS.Values)
            {
                _outputs.TryAdd(node.OutputName, node.Type);
            }
#endif
        }
    }
}
