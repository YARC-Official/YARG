using MoonscraperChartEditor.Song.IO;
using System;
using System.IO;
using System.Linq;
using YARG.Core.Audio;
using YARG.Core.Chart;
using YARG.Core.Extensions;
using YARG.Core.IO;
using YARG.Core.IO.Ini;
using YARG.Core.Logging;
using YARG.Core.Song.Cache;
using YARG.Core.Venue;

namespace YARG.Core.Song
{
    public sealed class SngEntry : IniSubEntry
    {
        private readonly uint _version;
        private readonly AbridgedFileInfo _sngInfo;
        private readonly ChartFormat _chartFormat;

        public override string Location => _sngInfo.FullName;
        public override string DirectoryActual => Path.GetDirectoryName(_sngInfo.FullName);
        public override DateTime GetAddDate() => _sngInfo.LastUpdatedTime.Date;
        public override EntryType SubType => EntryType.Sng;
        public override ulong SongLengthMilliseconds { get; }

        private SngEntry(SngFile sngFile, ChartFormat format, IniSection modifiers, in AvailableParts parts, in HashWrapper hash, in SongMetadata metadata, in LoaderSettings settings)
            : base(in metadata, in parts, in hash, in settings, modifiers)
        {
            _version = sngFile.Version;
            _sngInfo = sngFile.Info;
            _chartFormat = format;
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

        private SngEntry(uint version, in AbridgedFileInfo sngInfo, ChartFormat format, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            _version = version;
            _sngInfo = sngInfo;
            _chartFormat = format;
            SongLengthMilliseconds = stream.Read<ulong>(Endianness.Little);
        }

        public override void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            // Validation block
            stream.Write(_sngInfo.LastUpdatedTime.ToBinary(), Endianness.Little);
            stream.Write(_version, Endianness.Little);
            stream.WriteByte((byte) _chartFormat);

            // Metadata block
            base.Serialize(stream, node);
            stream.Write(SongLengthMilliseconds, Endianness.Little);
        }

        public override SongChart? LoadChart()
        {
            if (!_sngInfo.IsStillValid())
            {
                return null;
            }

            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
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

            string file = CHART_FILE_TYPES[(int) _chartFormat].Filename;
            using var stream = sngFile[file].CreateStream(sngFile);
            if (_chartFormat == ChartFormat.Mid || _chartFormat == ChartFormat.Midi)
            {
                return SongChart.FromMidi(in parseSettings, MidFileLoader.LoadMidiFile(stream));
            }

            using var reader = new StreamReader(stream);
            return SongChart.FromDotChart(in parseSettings, reader.ReadToEnd());
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _sngInfo.FullName);
                return null;
            }
            return CreateAudioMixer(speed, volume, sngFile, ignoreStems);
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                YargLogger.LogFormatError("Failed to load sng file {0}", _sngInfo.FullName);
                return null;
            }

            foreach (var filename in PREVIEW_FILES)
            {
                if (sngFile.TryGetValue(filename, out var listing))
                {
                    string fakename = Path.Combine(_sngInfo.FullName, filename);
                    var stream = listing.CreateStream(sngFile);
                    var mixer = GlobalAudioHandler.LoadCustomFile(fakename, stream, speed, 0, SongStem.Preview);
                    if (mixer == null)
                    {
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load preview file {0}!", fakename);
                        return null;
                    }
                    return mixer;
                }
            }

            return CreateAudioMixer(speed, 0, sngFile, SongStem.Crowd);
        }

        public override YARGImage? LoadAlbumData()
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
                return null;

            if (sngFile.TryGetValue(Cover, out var cover))
            {
                var image = YARGImage.Load(in cover, sngFile);
                if (image != null)
                {
                    return image;
                }
                YargLogger.LogFormatError("SNG Image mapped to {0} failed to load", Cover);
            }

            foreach (string albumFile in ALBUMART_FILES)
            {
                if (sngFile.TryGetValue(albumFile, out var listing))
                {
                    var image = YARGImage.Load(in listing, sngFile);
                    if (image != null)
                    {
                        return image;
                    }
                    YargLogger.LogFormatError("SNG Image mapped to {0} failed to load", albumFile);
                }
            }
            return null;
        }

        public override BackgroundResult? LoadBackground(BackgroundType options)
        {
            var sngFile = SngFile.TryLoadFromFile(_sngInfo);
            if (sngFile == null)
            {
                return null;
            }

            if ((options & BackgroundType.Yarground) > 0)
            {
                if (sngFile.TryGetValue(YARGROUND_FULLNAME, out var listing))
                {
                    var stream = listing.CreateStream(sngFile);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }

                string file = Path.ChangeExtension(_sngInfo.FullName, YARGROUND_EXTENSION);
                if (File.Exists(file))
                {
                    var stream = File.OpenRead(file);
                    return new BackgroundResult(BackgroundType.Yarground, stream);
                }
            }

            if ((options & BackgroundType.Video) > 0)
            {
                if (sngFile.TryGetValue(Video, out var video))
                {
                    var stream = video.CreateStream(sngFile);
                    return new BackgroundResult(BackgroundType.Video, stream);
                }

                foreach (var stem in BACKGROUND_FILENAMES)
                {
                    foreach (var format in VIDEO_EXTENSIONS)
                    {
                        string name = stem + format;
                        if (sngFile.TryGetValue(name, out var listing))
                        {
                            var stream = listing.CreateStream(sngFile);
                            return new BackgroundResult(BackgroundType.Video, stream);
                        }
                    }
                }

                foreach (var format in VIDEO_EXTENSIONS)
                {
                    string file = Path.ChangeExtension(_sngInfo.FullName, format);
                    if (File.Exists(file))
                    {
                        var stream = File.OpenRead(file);
                        return new BackgroundResult(BackgroundType.Video, stream);
                    }
                }
            }

            if ((options & BackgroundType.Image) > 0)
            {
                if (sngFile.TryGetValue(Background, out var listing) || TryGetRandomBackgroundImage(sngFile, out listing))
                {
                    var image = YARGImage.Load(in listing, sngFile);
                    if (image != null)
                    {
                        return new BackgroundResult(image);
                    }
                }

                // Fallback to a potential external image mapped specifically to the sng
                foreach (var format in IMAGE_EXTENSIONS)
                {
                    var file = new FileInfo(Path.ChangeExtension(_sngInfo.FullName, format));
                    if (file.Exists)
                    {
                        var image = YARGImage.Load(file);
                        if (image != null)
                        {
                            return new BackgroundResult(image);
                        }
                    }
                }
            }

            return null;
        }

        public override FixedArray<byte> LoadMiloData()
        {
            return FixedArray<byte>.Null;
        }

        private StemMixer? CreateAudioMixer(float speed, double volume, SngFile sngFile, params SongStem[] ignoreStems)
        {
            bool clampStemVolume = Metadata.Source.Str.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Failed to create mixer");
                return null;
            }

            foreach (var stem in IniAudio.SupportedStems)
            {
                var stemEnum = AudioHelpers.SupportedStems[stem];
                if (ignoreStems.Contains(stemEnum))
                    continue;

                foreach (var format in IniAudio.SupportedFormats)
                {
                    var file = stem + format;
                    if (sngFile.TryGetValue(file, out var listing))
                    {
                        var stream = listing.CreateStream(sngFile);
                        if (mixer.AddChannel(stemEnum, stream))
                        {
                            // No duplicates
                            break;
                        }
                        stream.Dispose();
                        YargLogger.LogFormatError("Failed to load stem file {0}", file);
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

        public static (ScanResult, SngEntry?) ProcessNewEntry(SngFile sng, in SngFileListing listing, ChartFormat format, string defaultPlaylist)
        {
            using var file = listing.LoadAllBytes(sng);
            var (result, parts, settings) = ProcessChartFile(file, format, sng.Metadata);
            if (result != ScanResult.Success)
            {
                return (result, null);
            }

            var hash = HashWrapper.Hash(file.ReadOnlySpan);
            var metadata = new SongMetadata(sng.Metadata, defaultPlaylist);
            var entry = new SngEntry(sng, format, sng.Metadata, in parts, in hash, in metadata, in settings);
            return (result, entry);
        }

        public static SngEntry? TryLoadFromCache(string filename, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var sngInfo = AbridgedFileInfo.TryParseInfo(filename, stream);
            if (sngInfo == null)
            {
                return null;
            }

            uint version = stream.Read<uint>(Endianness.Little);
            var sngFile = SngFile.TryLoadFromFile(sngInfo.Value);
            if (sngFile == null || sngFile.Version != version)
            {
                // TODO: Implement Update-in-place functionality
                return null;
            }

            byte chartTypeIndex = (byte) stream.ReadByte();
            return chartTypeIndex < CHART_FILE_TYPES.Length ? new SngEntry(sngFile.Version, sngInfo.Value, (ChartFormat) chartTypeIndex, stream, strings) : null;
        }

        public static SngEntry? LoadFromCache_Quick(string filename, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
        {
            var sngInfo = new AbridgedFileInfo(filename, stream);
            uint version = stream.Read<uint>(Endianness.Little);
            byte chartTypeIndex = (byte) stream.ReadByte();
            return chartTypeIndex < CHART_FILE_TYPES.Length ? new SngEntry(version, sngInfo, (ChartFormat) chartTypeIndex, stream, strings) : null;
        }
    }
}
