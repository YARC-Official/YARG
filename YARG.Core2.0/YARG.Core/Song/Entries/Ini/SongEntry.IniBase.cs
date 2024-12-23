using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Song.Cache;
using YARG.Core.Song.Preparsers;

namespace YARG.Core.Song
{
    public static class IniAudio
    {
        public static readonly string[] SupportedStems = { "song", "guitar", "bass", "rhythm", "keys", "vocals", "vocals_1", "vocals_2", "drums", "drums_1", "drums_2", "drums_3", "drums_4", "crowd", };
        public static readonly string[] SupportedFormats = { ".opus", ".ogg", ".mp3", ".wav", ".aiff", };
        private static readonly HashSet<string> SupportedAudioFiles = new();

        static IniAudio()
        {
            foreach (string stem in SupportedStems)
                foreach (string format in SupportedFormats)
                    SupportedAudioFiles.Add(stem + format);
        }

        public static bool IsAudioFile(string file)
        {
            return SupportedAudioFiles.Contains(file);
        }
    }

    public abstract class IniSubEntry : SongEntry
    {
        public static readonly (string Filename, ChartFormat Format)[] CHART_FILE_TYPES =
        {
            ("notes.mid"  , ChartFormat.Mid),
            ("notes.midi" , ChartFormat.Midi),
            ("notes.chart", ChartFormat.Chart),
        };

        protected static readonly string[] ALBUMART_FILES;
        protected static readonly string[] PREVIEW_FILES;

        static IniSubEntry()
        {
            ALBUMART_FILES = new string[IMAGE_EXTENSIONS.Length];
            for (int i = 0; i < ALBUMART_FILES.Length; i++)
            {
                ALBUMART_FILES[i] = "album" + IMAGE_EXTENSIONS[i];
            }

            PREVIEW_FILES = new string[IniAudio.SupportedFormats.Length];
            for (int i = 0; i < PREVIEW_FILES.Length; i++)
            {
                PREVIEW_FILES[i] = "preview" + IniAudio.SupportedFormats[i];
            }
        }

        public readonly string Background;
        public readonly string Video;
        public readonly string Cover;
        
        public override string Year { get; }
        public override int YearAsNumber { get; }
        public override bool LoopVideo { get; }

        protected IniSubEntry(in SongMetadata metadata, in AvailableParts parts, in HashWrapper hash, in LoaderSettings settings, IniSection modifiers)
            : base(in metadata, in parts, in hash, in settings)
        {
            (Year, YearAsNumber) = ParseYear(Metadata.Year);
            if (modifiers.TryGet("background", out Background))
            {
                string ext = Path.GetExtension(Background.Trim('\"')).ToLower();
                Background = IMAGE_EXTENSIONS.Contains(ext) ? Background.ToLowerInvariant() : string.Empty;
            }

            if (modifiers.TryGet("video", out Video))
            {
                string ext = Path.GetExtension(Video.Trim('\"')).ToLower();
                Video = VIDEO_EXTENSIONS.Contains(ext) ? Video.ToLowerInvariant() : string.Empty;
            }

            if (modifiers.TryGet("cover", out Cover))
            {
                string ext = Path.GetExtension(Cover.Trim('\"')).ToLower();
                Cover = IMAGE_EXTENSIONS.Contains(ext) ? Cover.ToLowerInvariant() : string.Empty;
            }
            LoopVideo = modifiers.TryGet("video_loop", out bool loop) && loop;
        }

        protected IniSubEntry(UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            (Year, YearAsNumber) = ParseYear(Metadata.Year);

            Background = stream.ReadString();
            Video = stream.ReadString();
            Cover = stream.ReadString();
            LoopVideo = stream.ReadBoolean();
        }

        public override void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            base.Serialize(stream, node);
            stream.Write(Background);
            stream.Write(Video);
            stream.Write(Cover);
            stream.Write(LoopVideo);
        }

        public override FixedArray<byte> LoadMiloData()
        {
            return FixedArray<byte>.Null;
        }

        protected static (ScanResult Result, AvailableParts Parts, LoaderSettings Settings) ProcessChartFile(in FixedArray<byte> file, ChartFormat format, IniSection modifiers)
        {
            DrumPreparseHandler drums = new()
            {
                Type = GetDrumTypeFromModifier(modifiers)
            };

            var parts = AvailableParts.Default;
            var settings = default(LoaderSettings);
            var results = default((ScanResult result, long resolution));
            if (format == ChartFormat.Chart)
            {
                if (YARGTextReader.IsUTF8(in file, out var byteContainer))
                {
                    results = ParseDotChart(ref byteContainer, modifiers, ref parts, drums);
                }
                else
                {
                    using var chars = YARGTextReader.ConvertToUTF16(in file, out var charContainer);
                    if (chars.IsAllocated)
                    {
                        results = ParseDotChart(ref charContainer, modifiers, ref parts, drums);
                    }
                    else
                    {
                        using var ints = YARGTextReader.ConvertToUTF32(in file, out var intContainer);
                        results = ParseDotChart(ref intContainer, modifiers, ref parts, drums);
                    }
                }
            }
            else // if (chartType == ChartType.Mid || chartType == ChartType.Midi) // Uncomment for any future file type
            {
                results = ParseDotMidi(in file, modifiers, ref parts, drums);
            }

            if (results.result != ScanResult.Success)
            {
                return (results.result, parts, settings);
            }

            SetDrums(ref parts, drums);
            if (!CheckScanValidity(in parts))
            {
                return (ScanResult.NoNotes, parts, settings);
            }

            if (!modifiers.Contains("name"))
            {
                return (ScanResult.NoName, parts, settings);
            }

            if (!modifiers.TryGet("hopo_frequency", out settings.HopoThreshold) || settings.HopoThreshold <= 0)
            {
                if (modifiers.TryGet("eighthnote_hopo", out bool eighthNoteHopo))
                {
                    settings.HopoThreshold = results.resolution / (eighthNoteHopo ? 2 : 3);
                }
                else if (modifiers.TryGet("hopofreq", out long hopoFreq))
                {
                    int denominator = hopoFreq switch
                    {
                        0 => 24,
                        1 => 16,
                        2 => 12,
                        3 => 8,
                        4 => 6,
                        5 => 4,
                        _ => throw new NotImplementedException($"Unhandled hopofreq value {hopoFreq}!")
                    };
                    settings.HopoThreshold = 4 * results.resolution / denominator;
                }
                else
                {
                    settings.HopoThreshold = results.resolution / 3;
                }

                if (format == ChartFormat.Chart)
                {
                    // With a 192 resolution, .chart has a HOPO threshold of 65 ticks, not 64,
                    // so we need to scale this factor to different resolutions (480 res = 162.5 threshold).
                    // Why?... idk, but I hate it.
                    const float DEFAULT_RESOLUTION = 192;
                    settings.HopoThreshold += (long) (results.resolution / DEFAULT_RESOLUTION);
                }
            }

            // .chart defaults to no sustain cutoff whatsoever if the ini does not define the value.
            // Since a failed `TryGet` sets the value to zero, we would need no additional work unless it's .mid
            if (!modifiers.TryGet("sustain_cutoff_threshold", out settings.SustainCutoffThreshold) && format != ChartFormat.Chart)
            {
                settings.SustainCutoffThreshold = results.resolution / 3;
            }

            if (format == ChartFormat.Mid || format == ChartFormat.Midi)
            {
                if (!modifiers.TryGet("multiplier_note", out settings.OverdiveMidiNote) || settings.OverdiveMidiNote != 103)
                {
                    settings.OverdiveMidiNote = 116;
                }
            }

            SetIntensities(modifiers, ref parts);
            return (ScanResult.Success, parts, settings);
        }

        private static (string Parsed, int AsNumber) ParseYear(string str)
        {
            for (int start = 0; start <= str.Length - MINIMUM_YEAR_DIGITS; ++start)
            {
                int curr = start;
                int number = 0;
                while (curr < str.Length && char.IsDigit(str[curr]))
                {
                    unchecked
                    {
                        number = 10 * number + str[curr] - '0';
                    }
                    ++curr;
                }

                if (curr >= start + MINIMUM_YEAR_DIGITS)
                {
                    return (str[start..curr], number);
                }
            }
            return (str, int.MaxValue);
        }

        protected static bool TryGetRandomBackgroundImage<TEnumerable, TValue>(TEnumerable collection, out TValue? value)
            where TEnumerable : IEnumerable<KeyValuePair<string, TValue>>
        {
            // Choose a valid image background present in the folder at random
            var images = new List<TValue>();
            foreach (var format in SongEntry.IMAGE_EXTENSIONS)
            {
                var (_, image) = collection.FirstOrDefault(node => node.Key == "bg" + format);
                if (image != null)
                {
                    images.Add(image);
                }
            }

            foreach (var (shortname, image) in collection)
            {
                if (!shortname.StartsWith("background"))
                {
                    continue;
                }

                foreach (var format in SongEntry.IMAGE_EXTENSIONS)
                {
                    if (shortname.EndsWith(format))
                    {
                        images.Add(image);
                        break;
                    }
                }
            }

            if (images.Count == 0)
            {
                value = default!;
                return false;
            }
            value = images[SongEntry.BACKROUND_RNG.Next(images.Count)];
            return true;
        }

        protected static DrumsType ParseDrumsType(in AvailableParts parts)
        {
            if (parts.FourLaneDrums.SubTracks > 0)
            {
                return DrumsType.FourLane;
            }
            if (parts.FiveLaneDrums.SubTracks > 0)
            {
                return DrumsType.FiveLane;
            }
            return DrumsType.Unknown;
        }

        private static (ScanResult result, long resolution) ParseDotChart<TChar>(ref YARGTextContainer<TChar> container, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            long resolution = 192;
            if (YARGChartFileReader.ValidateTrack(ref container, YARGChartFileReader.HEADERTRACK))
            {
                var chartMods = YARGChartFileReader.ExtractModifiers(ref container);
                if (chartMods.Remove("Resolution", out var resolutions))
                {
                    unsafe
                    {
                        var mod = resolutions[0];
                        resolution = mod.Buffer[0];
                        if (resolution < 1)
                        {
                            return (ScanResult.InvalidResolution, 0);
                        }
                    }
                }
                modifiers.Append(chartMods);
            }

            while (YARGChartFileReader.IsStartOfTrack(in container))
            {
                if (!TraverseChartTrack(ref container, drums, ref parts))
                {
                    if (YARGTextReader.SkipLinesUntil(ref container, TextConstants<TChar>.CLOSE_BRACE))
                    {
                        YARGTextReader.GotoNextLine(ref container);
                    }
                }
            }

            if (drums.Type == DrumsType.Unknown && drums.ValidatedDiffs > 0)
            {
                drums.Type = DrumsType.FourLane;
            }
            return (ScanResult.Success, resolution);
        }
        private static (ScanResult result, long resolution) ParseDotMidi(in FixedArray<byte> file, IniSection modifiers, ref AvailableParts parts, DrumPreparseHandler drums)
        {
            bool usePro = !modifiers.TryGet("pro_drums", out bool proDrums) || proDrums;
            if (drums.Type == DrumsType.Unknown)
            {
                if (usePro)
                {
                    drums.Type = DrumsType.UnknownPro;
                }
            }
            else if (drums.Type == DrumsType.FourLane && usePro)
            {
                drums.Type = DrumsType.ProDrums;
            }
            return ParseMidi(in file, drums, ref parts);
        }

        /// <returns>Whether the track was fully traversed</returns>
        private static unsafe bool TraverseChartTrack<TChar>(ref YARGTextContainer<TChar> container, DrumPreparseHandler drums, ref AvailableParts parts)
            where TChar : unmanaged, IEquatable<TChar>, IConvertible
        {
            if (!YARGChartFileReader.ValidateInstrument(ref container, out var instrument, out var difficulty))
            {
                return false;
            }

            return instrument switch
            {
                Instrument.FiveFretGuitar => ChartPreparser.Traverse(ref container, difficulty, ref parts.FiveFretGuitar, &ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretBass => ChartPreparser.Traverse(ref container, difficulty, ref parts.FiveFretBass, &ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretRhythm => ChartPreparser.Traverse(ref container, difficulty, ref parts.FiveFretRhythm, &ChartPreparser.ValidateFiveFret),
                Instrument.FiveFretCoopGuitar => ChartPreparser.Traverse(ref container, difficulty, ref parts.FiveFretCoopGuitar, &ChartPreparser.ValidateFiveFret),
                Instrument.SixFretGuitar => ChartPreparser.Traverse(ref container, difficulty, ref parts.SixFretGuitar, &ChartPreparser.ValidateSixFret),
                Instrument.SixFretBass => ChartPreparser.Traverse(ref container, difficulty, ref parts.SixFretBass, &ChartPreparser.ValidateSixFret),
                Instrument.SixFretRhythm => ChartPreparser.Traverse(ref container, difficulty, ref parts.SixFretRhythm, &ChartPreparser.ValidateSixFret),
                Instrument.SixFretCoopGuitar => ChartPreparser.Traverse(ref container, difficulty, ref parts.SixFretCoopGuitar, &ChartPreparser.ValidateSixFret),
                Instrument.Keys => ChartPreparser.Traverse(ref container, difficulty, ref parts.Keys, &ChartPreparser.ValidateFiveFret),
                Instrument.FourLaneDrums => drums.ParseChart(ref container, difficulty),
                _ => false,
            };
        }

        private static DrumsType GetDrumTypeFromModifier(IniSection modifiers)
        {
            if (!modifiers.TryGet("five_lane_drums", out bool fivelane))
                return DrumsType.Unknown;
            return fivelane ? DrumsType.FiveLane : DrumsType.FourLane;
        }

        private static void SetIntensities(IniSection modifiers, ref AvailableParts parts)
        {
            if (modifiers.TryGet("diff_band", out int intensity))
            {
                parts.BandDifficulty.Intensity = (sbyte) intensity;
                if (intensity != -1)
                {
                    parts.BandDifficulty.SubTracks = 1;
                }
            }

            if (modifiers.TryGet("diff_guitar", out intensity))
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = parts.FiveFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bass", out intensity))
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = parts.FiveFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm", out intensity))
            {
                parts.FiveFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop", out intensity))
            {
                parts.FiveFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitarghl", out intensity))
            {
                parts.SixFretGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_bassghl", out intensity))
            {
                parts.SixFretBass.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_rhythm_ghl", out intensity))
            {
                parts.SixFretRhythm.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_guitar_coop_ghl", out intensity))
            {
                parts.SixFretCoopGuitar.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_keys", out intensity))
            {
                parts.ProKeys.Intensity = parts.Keys.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums", out intensity))
            {
                parts.FourLaneDrums.Intensity = (sbyte) intensity;
                parts.ProDrums.Intensity = (sbyte) intensity;
                parts.FiveLaneDrums.Intensity = (sbyte) intensity;
                parts.EliteDrums.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_drums_real", out intensity) && intensity != -1)
            {
                parts.ProDrums.Intensity = (sbyte) intensity;
                parts.EliteDrums.Intensity = (sbyte) intensity;
                if (parts.FourLaneDrums.Intensity == -1)
                {
                    parts.FourLaneDrums.Intensity = parts.ProDrums.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real", out intensity) && intensity != -1)
            {
                parts.ProGuitar_22Fret.Intensity = parts.ProGuitar_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretGuitar.Intensity == -1)
                {
                    parts.FiveFretGuitar.Intensity = parts.ProGuitar_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real", out intensity) && intensity != -1)
            {
                parts.ProBass_22Fret.Intensity = parts.ProBass_17Fret.Intensity = (sbyte) intensity;
                if (parts.FiveFretBass.Intensity == -1)
                {
                    parts.FiveFretBass.Intensity = parts.ProBass_17Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_guitar_real_22", out intensity) && intensity != -1)
            {
                parts.ProGuitar_22Fret.Intensity = (sbyte) intensity;
                if (parts.ProGuitar_17Fret.Intensity == -1)
                {
                    parts.ProGuitar_17Fret.Intensity = parts.ProGuitar_22Fret.Intensity;
                }

                if (parts.FiveFretGuitar.Intensity == -1)
                {
                    parts.FiveFretGuitar.Intensity = parts.ProGuitar_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_bass_real_22", out intensity) && intensity != -1)
            {
                parts.ProBass_22Fret.Intensity = (sbyte) intensity;
                if (parts.ProBass_17Fret.Intensity == -1)
                {
                    parts.ProBass_17Fret.Intensity = parts.ProBass_22Fret.Intensity;
                }

                if (parts.FiveFretBass.Intensity == -1)
                {
                    parts.FiveFretBass.Intensity = parts.ProBass_22Fret.Intensity;
                }
            }

            if (modifiers.TryGet("diff_keys_real", out intensity) && intensity != -1)
            {
                parts.ProKeys.Intensity = (sbyte) intensity;
                if (parts.Keys.Intensity == -1)
                {
                    parts.Keys.Intensity = parts.ProKeys.Intensity;
                }
            }

            if (modifiers.TryGet("diff_vocals", out intensity))
            {
                parts.HarmonyVocals.Intensity = parts.LeadVocals.Intensity = (sbyte) intensity;
            }

            if (modifiers.TryGet("diff_vocals_harm", out intensity) && intensity != -1)
            {
                parts.HarmonyVocals.Intensity = (sbyte) intensity;
                if (parts.LeadVocals.Intensity == -1)
                {
                    parts.LeadVocals.Intensity = parts.HarmonyVocals.Intensity;
                }
            }
        }
    }
}
