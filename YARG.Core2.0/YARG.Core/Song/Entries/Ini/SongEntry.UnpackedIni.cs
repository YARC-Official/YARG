using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Audio;
using YARG.Core.Venue;
using System.Linq;
using YARG.Core.Logging;
using YARG.Core.Extensions;
using MoonscraperChartEditor.Song.IO;
using YARG.Core.Chart;

namespace YARG.Core.Song
{
    public sealed class UnpackedIniEntry : IniSubEntry
    {
        private readonly AbridgedFileInfo _chartFile;
        private readonly ChartFormat _chartFormat;
        private readonly AbridgedFileInfo? _iniFile;

        public override string Location { get; }
        public override string DirectoryActual => Location;
        public override DateTime GetAddDate() => _chartFile.LastUpdatedTime.Date;
        public override EntryType SubType => EntryType.Ini;
        public override ulong SongLengthMilliseconds { get; }

        private UnpackedIniEntry(string directory, FileInfo chartInfo, ChartFormat format, in AbridgedFileInfo? iniFile, IniSection modifiers, in AvailableParts parts, in HashWrapper hash, in SongMetadata metadata, in LoaderSettings settings)
            : base(in metadata, in parts, in hash, in settings, modifiers)
        {
            Location = directory;
            _chartFile = new AbridgedFileInfo(chartInfo);
            _chartFormat = format;
            _iniFile = iniFile;

            if (!modifiers.TryGet("song_length", out ulong songLength))
            {
                using var mixer = LoadAudio(0, 0);
                if (mixer != null)
                {
                    songLength = (ulong) (mixer.Length * SongMetadata.MILLISECOND_FACTOR);
                }
            }
            SongLengthMilliseconds = songLength;
        }

        private UnpackedIniEntry(string directory, in AbridgedFileInfo chartInfo, ChartFormat format, in AbridgedFileInfo? iniFile, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            Location = directory;
            _chartFile = chartInfo;
            _chartFormat = format;
            _iniFile = iniFile;
            SongLengthMilliseconds = stream.Read<ulong>(Endianness.Little);
        }

        public override void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            // Validation block
            stream.WriteByte((byte) _chartFormat);
            stream.Write(_chartFile.LastUpdatedTime.ToBinary(), Endianness.Little);
            stream.Write(_iniFile != null);
            if (_iniFile != null)
            {
                stream.Write(_iniFile.Value.LastUpdatedTime.ToBinary(), Endianness.Little);
            }

            // Metadata block
            base.Serialize(stream, node);
            stream.Write(SongLengthMilliseconds, Endianness.Little);
        }

        public override SongChart? LoadChart()
        {
            if (!_chartFile.IsStillValid())
            {
                return null;
            }

            if (_iniFile != null ? !_iniFile.Value.IsStillValid() : File.Exists(Path.Combine(Location, "song.ini")))
            {
                return null;
            }

            var parseSettings = new ParseSettings()
            {
                HopoThreshold = Settings.HopoThreshold,
                SustainCutoffThreshold = Settings.SustainCutoffThreshold,
                StarPowerNote = Settings.OverdiveMidiNote,
                DrumsType = ParseDrumsType(in Parts),
                ChordHopoCancellation = _chartFormat != ChartFormat.Chart
            };

            using var stream = new FileStream(_chartFile.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            if (_chartFormat == ChartFormat.Mid || _chartFormat == ChartFormat.Midi)
            {
                return SongChart.FromMidi(in parseSettings, MidFileLoader.LoadMidiFile(stream));
            }

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(in parseSettings, reader.ReadToEnd());
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = Metadata.Source.Str.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer!");
                return null;
            }

            var subFiles = GetSubFiles();
            foreach (var stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                if (ignoreStems.Contains(stemEnum))
                    continue;

                foreach (var format in IniAudio.SupportedFormats)
                {
                    var audioFile = stem + format;
                    if (subFiles.TryGetValue(audioFile, out var info))
                    {
                        var stream = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
                        if (mixer.AddChannel(stemEnum, stream))
                        {
                            // No duplicates
                            break;
                        }
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load stem file {0}", info.FullName);
                    }
                }
            }

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Failed to add any stems!");
                mixer.Dispose();
                return null;
            }

            if (GlobalAudioHandler.LogMixerStatus)
            {
                YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
            }
            return mixer;
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            foreach (var filename in PREVIEW_FILES)
            {
                var audioFile = Path.Combine(Location, filename);
                if (File.Exists(audioFile))
                {
                    return GlobalAudioHandler.LoadCustomFile(audioFile, speed, 0, SongStem.Preview);
                }
            }
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            var subFiles = GetSubFiles();
            if (!string.IsNullOrEmpty(Cover) && subFiles.TryGetValue(Cover, out var cover))
            {
                var image = YARGImage.Load(cover);
                if (image != null)
                {
                    return image;
                }
                YargLogger.LogFormatError("Image at {0} failed to load", cover.FullName);
            }

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (subFiles.TryGetValue(albumFile, out var info))
                {
                    var image = YARGImage.Load(info);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("Image at {0} failed to load", info.FullName);
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            var subFiles = GetSubFiles();
            if ((options & BackgroundType.Yarground) > 0)
            {
                if (subFiles.TryGetValue("bg.yarground", out var file))
                {
                    var stream = File.OpenRead(file.FullName);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (subFiles.TryGetValue(Video, out var video))
                {
                    var stream = File.OpenRead(video.FullName);
                    return new BackgroundResult(BackgroundType.Video, stream);
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        if (subFiles.TryGetValue(stem + format, out var info))
                        {
                            var stream = File.OpenRead(info.FullName);
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (subFiles.TryGetValue(Background, out var file) || TryGetRandomBackgroundImage(subFiles, out file))
                {
                    var image = YARGImage.Load(file!);
                    if (image != null)
                    {
                        return new BackgroundResult(image);
                    }
                }
            }
            return null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
            return FixedArray<byte>.Null;
        }

        private Dictionary<string, FileInfo> GetSubFiles()
        {
            Dictionary<string, FileInfo> files = new();
            var dirInfo = new DirectoryInfo(Location);
            if (dirInfo.Exists)
            {
                foreach (var file in dirInfo.EnumerateFiles())
                {
                    files.Add(file.Name.ToLower(), file);
                }
            }
            return files;
        }

        public static (ScanResult, UnpackedIniEntry?) ProcessNewEntry(string chartDirectory, FileInfo chartInfo, ChartFormat format, FileInfo? iniFile, string defaultPlaylist)
        {
            IniSection iniModifiers;
            AbridgedFileInfo? iniFileInfo = null;
            if (iniFile != null)
            {
                if ((iniFile.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) > 0)
                {
                    return (ScanResult.IniNotDownloaded, null);
                }

                iniModifiers = SongIniHandler.ReadSongIniFile(iniFile.FullName);
                iniFileInfo = new AbridgedFileInfo(iniFile);
            }
            else
            {
                iniModifiers = new();
            }

            if ((chartInfo.Attributes & AbridgedFileInfo.RECALL_ON_DATA_ACCESS) > 0)
            {
                return (ScanResult.ChartNotDownloaded, null);
            }

            using var file = FixedArray<byte>.Load(chartInfo.FullName);
            var (result, parts, settings) = ProcessChartFile(file, format, iniModifiers);
            if (result != ScanResult.Success)
            {
                return (result, null);
            }

            var hash = HashWrapper.Hash(file.ReadOnlySpan);
            var metadata = new SongMetadata(iniModifiers, defaultPlaylist);
            var entry = new UnpackedIniEntry(chartDirectory, chartInfo, format, in iniFileInfo, iniModifiers, in parts, in hash, in metadata, in settings);
            return (result, entry);
        }

        public static UnpackedIniEntry? TryLoadFromCache(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            byte chartTypeIndex = (byte) stream.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = CHART_FILE_TYPES[chartTypeIndex];
            var chartInfo = AbridgedFileInfo.TryParseInfo(Path.Combine(directory, chart.Filename), stream);
            if (chartInfo == null)
            {
                return null;
            }

            string iniFile = Path.Combine(directory, "song.ini");
            AbridgedFileInfo? iniInfo = null;
            if (stream.ReadBoolean())
            {
                iniInfo = AbridgedFileInfo.TryParseInfo(iniFile, stream);
                if (iniInfo == null)
                {
                    return null;
                }
            }
            else if (File.Exists(iniFile))
            {
                return null;
            }
            return new UnpackedIniEntry(directory, chartInfo.Value, chart.Format, in iniInfo, stream, strings);
        }

        public static UnpackedIniEntry? IniFromCache_Quick(string directory, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            byte chartTypeIndex = (byte) stream.ReadByte();
            if (chartTypeIndex >= CHART_FILE_TYPES.Length)
            {
                return null;
            }

            var chart = CHART_FILE_TYPES[chartTypeIndex];
            var chartInfo = new AbridgedFileInfo(Path.Combine(directory, chart.Filename), stream);
            AbridgedFileInfo? iniInfo = stream.ReadBoolean() ? new AbridgedFileInfo(Path.Combine(directory, "song.ini"), stream) : null;
            return new UnpackedIniEntry(directory, chartInfo, chart.Format, in iniInfo, stream, strings);
        }
    }
}
