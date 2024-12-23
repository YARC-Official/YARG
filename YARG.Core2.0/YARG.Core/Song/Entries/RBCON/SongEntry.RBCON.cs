using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YARG.Core.Chart;
using YARG.Core.Song.Cache;
using YARG.Core.IO;
using YARG.Core.Song.Preparsers;
using Melanchall.DryWetMidi.Core;
using YARG.Core.Extensions;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Core.Song
{
    public abstract class RBCONEntry : SongEntry
    {
        private const long NOTE_SNAP_THRESHOLD = 10;

        public readonly RBMetadata RBMetadata;
        public readonly RBCONDifficulties RBDifficulties;

        public readonly AbridgedFileInfo? UpdateMidi;
        public readonly RBProUpgrade? Upgrade;

        public readonly AbridgedFileInfo? UpdateMogg;
        public readonly AbridgedFileInfo? UpdateMilo;
        public readonly AbridgedFileInfo? UpdateImage;

        public string RBSongId => RBMetadata.SongID;
        public int RBBandDiff => RBDifficulties.Band;

        public override string Year { get; }
        public override int YearAsNumber { get; }
        public override ulong SongLengthMilliseconds { get; }
        public override bool LoopVideo => false;

        protected abstract DateTime MidiLastUpdate { get; }

        protected RBCONEntry(in ScanNode info, CONModification modification, in HashWrapper hash)
            : base(in info.Metadata, in info.Parts, in hash, in info.Settings)
        {
            Year = info.Metadata.Year;
            YearAsNumber = info.YearAsNumber;
            SongLengthMilliseconds = info.SongLength;
            RBMetadata = info.RBMetadata;
            RBDifficulties = info.Difficulties;
            UpdateMidi = modification.Midi;
            UpdateMogg = modification.Mogg;
            UpdateImage = modification.Image;
            UpdateMilo = modification.Milo;
            Upgrade = modification.UpgradeNode;
        }

        protected RBCONEntry(AbridgedFileInfo? updateMidi, RBProUpgrade? upgrade, UnmanagedMemoryStream stream, CategoryCacheStrings strings)
            : base(stream, strings)
        {
            UpdateMidi = updateMidi;
            Upgrade = upgrade;

            YearAsNumber = stream.Read<int>(Endianness.Little);
            SongLengthMilliseconds = stream.Read<ulong>(Endianness.Little);

            UpdateMogg = stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            UpdateMilo = stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            UpdateImage = stream.ReadBoolean() ? new AbridgedFileInfo(stream.ReadString(), false) : null;
            unsafe
            {
                RBDifficulties = *(RBCONDifficulties*) stream.PositionPointer;
                stream.Position += sizeof(RBCONDifficulties);
            }
            RBMetadata = new RBMetadata(stream);

            Year = YearAsNumber != int.MaxValue ? YearAsNumber.ToString("D4") : Metadata.Year;
        }

        public override void Serialize(MemoryStream stream, CategoryCacheWriteNode node)
        {
            base.Serialize(stream, node);
            stream.Write(YearAsNumber, Endianness.Little);
            stream.Write(SongLengthMilliseconds, Endianness.Little);
            WriteUpdateInfo(UpdateMogg, stream);
            WriteUpdateInfo(UpdateMilo, stream);
            WriteUpdateInfo(UpdateImage, stream);
            unsafe
            {
                fixed (RBCONDifficulties* ptr = &RBDifficulties)
                {
                    var span = new ReadOnlySpan<byte>(ptr, sizeof(RBCONDifficulties));
                    stream.Write(span);
                }
            }
            RBMetadata.Serialize(stream);
        }

        public override DateTime GetAddDate()
        {
            var lastUpdateTime = MidiLastUpdate;
            if (UpdateMidi != null)
            {
                if (UpdateMidi.Value.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = UpdateMidi.Value.LastUpdatedTime;
                }
            }

            if (Upgrade != null)
            {
                if (Upgrade.LastUpdatedTime > lastUpdateTime)
                {
                    lastUpdateTime = Upgrade.LastUpdatedTime;
                }
            }
            return lastUpdateTime.Date;
        }

        public override SongChart? LoadChart()
        {
            MidiFile midi;
            var readingSettings = MidiSettingsLatin1.Instance; // RBCONs are always Latin-1
            // Read base MIDI
            using (var midiStream = GetMidiStream())
            {
                if (midiStream == null)
                {
                    return null;
                }
                midi = MidiFile.Read(midiStream, readingSettings);
            }

            // Merge update MIDI
            if (UpdateMidi != null)
            {
                if (!UpdateMidi.Value.IsStillValid(false))
                {
                    return null;
                }

                using var midiStream = new FileStream(UpdateMidi.Value.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            // Merge upgrade MIDI
            if (Upgrade != null)
            {
                using var midiStream = Upgrade.GetUpgradeMidiStream();
                if (midiStream == null)
                {
                    return null;
                }
                var update = MidiFile.Read(midiStream, readingSettings);
                midi.Merge(update);
            }

            var parseSettings = new ParseSettings()
            {
                HopoThreshold = Settings.HopoThreshold,
                SustainCutoffThreshold = Settings.SustainCutoffThreshold,
                StarPowerNote = Settings.OverdiveMidiNote,
                DrumsType = DrumsType.FourLane,
                ChordHopoCancellation = true
            };
            return SongChart.FromMidi(in parseSettings, midi);
        }

        public override StemMixer? LoadAudio(float speed, double volume, params SongStem[] ignoreStems)
        {
            var stream = GetMoggStream();
            if (stream == null)
            {
                return null;
            }

            int version = stream.Read<int>(Endianness.Little);
            if (version is not 0x0A and not 0xF0)
            {
                YargLogger.LogError("Original unencrypted mogg replaced by an encrypted mogg!");
                stream.Dispose();
                return null;
            }

            int start = stream.Read<int>(Endianness.Little);
            stream.Seek(start, SeekOrigin.Begin);

            bool clampStemVolume = Metadata.Source.Str.ToLowerInvariant() == "yarg";
            var mixer = GlobalAudioHandler.CreateMixer(ToString(), stream, speed, volume, clampStemVolume);
            if (mixer == null)
            {
                YargLogger.LogError("Mogg failed to load!");
                stream.Dispose();
                return null;
            }


            if (RBMetadata.Indices.Drums.Length > 0 && !ignoreStems.Contains(SongStem.Drums))
            {
                switch (RBMetadata.Indices.Drums.Length)
                {
                    //drum (0 1): stereo kit --> (0 1)
                    case 1:
                    case 2:
                        mixer.AddChannel(SongStem.Drums, RBMetadata.Indices.Drums, RBMetadata.Panning.Drums!);
                        break;
                    //drum (0 1 2): mono kick, stereo snare/kit --> (0) (1 2)
                    case 3:
                        mixer.AddChannel(SongStem.Drums1, RBMetadata.Indices.Drums[0..1], RBMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, RBMetadata.Indices.Drums[1..3], RBMetadata.Panning.Drums[2..6]);
                        break;
                    //drum (0 1 2 3): mono kick, mono snare, stereo kit --> (0) (1) (2 3)
                    case 4:
                        mixer.AddChannel(SongStem.Drums1, RBMetadata.Indices.Drums[0..1], RBMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, RBMetadata.Indices.Drums[1..2], RBMetadata.Panning.Drums[2..4]);
                        mixer.AddChannel(SongStem.Drums3, RBMetadata.Indices.Drums[2..4], RBMetadata.Panning.Drums[4..8]);
                        break;
                    //drum (0 1 2 3 4): mono kick, stereo snare, stereo kit --> (0) (1 2) (3 4)
                    case 5:
                        mixer.AddChannel(SongStem.Drums1, RBMetadata.Indices.Drums[0..1], RBMetadata.Panning.Drums![0..2]);
                        mixer.AddChannel(SongStem.Drums2, RBMetadata.Indices.Drums[1..3], RBMetadata.Panning.Drums[2..6]);
                        mixer.AddChannel(SongStem.Drums3, RBMetadata.Indices.Drums[3..5], RBMetadata.Panning.Drums[6..10]);
                        break;
                    //drum (0 1 2 3 4 5): stereo kick, stereo snare, stereo kit --> (0 1) (2 3) (4 5)
                    case 6:
                        mixer.AddChannel(SongStem.Drums1, RBMetadata.Indices.Drums[0..2], RBMetadata.Panning.Drums![0..4]);
                        mixer.AddChannel(SongStem.Drums2, RBMetadata.Indices.Drums[2..4], RBMetadata.Panning.Drums[4..8]);
                        mixer.AddChannel(SongStem.Drums3, RBMetadata.Indices.Drums[4..6], RBMetadata.Panning.Drums[8..12]);
                        break;
                }
            }

            if (RBMetadata.Indices.Bass.Length > 0 && !ignoreStems.Contains(SongStem.Bass))
                mixer.AddChannel(SongStem.Bass, RBMetadata.Indices.Bass, RBMetadata.Panning.Bass!);

            if (RBMetadata.Indices.Guitar.Length > 0 && !ignoreStems.Contains(SongStem.Guitar))
                mixer.AddChannel(SongStem.Guitar, RBMetadata.Indices.Guitar, RBMetadata.Panning.Guitar!);

            if (RBMetadata.Indices.Keys.Length > 0 && !ignoreStems.Contains(SongStem.Keys))
                mixer.AddChannel(SongStem.Keys, RBMetadata.Indices.Keys, RBMetadata.Panning.Keys!);

            if (RBMetadata.Indices.Vocals.Length > 0 && !ignoreStems.Contains(SongStem.Vocals))
                mixer.AddChannel(SongStem.Vocals, RBMetadata.Indices.Vocals, RBMetadata.Panning.Vocals!);

            if (RBMetadata.Indices.Track.Length > 0 && !ignoreStems.Contains(SongStem.Song))
                mixer.AddChannel(SongStem.Song, RBMetadata.Indices.Track, RBMetadata.Panning.Track!);

            if (RBMetadata.Indices.Crowd.Length > 0 && !ignoreStems.Contains(SongStem.Crowd))
                mixer.AddChannel(SongStem.Crowd, RBMetadata.Indices.Crowd, RBMetadata.Panning.Crowd!);

            if (mixer.Channels.Count == 0)
            {
                YargLogger.LogError("Failed to add any stems!");
                stream.Dispose();
                mixer.Dispose();
                return null;
            }
            YargLogger.LogFormatInfo("Loaded {0} stems", mixer.Channels.Count);
            return mixer;
        }

        public override StemMixer? LoadPreviewAudio(float speed)
        {
            return LoadAudio(speed, 0, SongStem.Crowd);
        }

        protected abstract Stream? GetMidiStream();
        protected abstract Stream? GetMoggStream();

        public struct ScanNode
        {
            public static readonly ScanNode Default = new()
            {
                Metadata = SongMetadata.Default,
                RBMetadata = RBMetadata.Default,
                Settings = LoaderSettings.Default,
                Parts = AvailableParts.Default,
                Difficulties = RBCONDifficulties.Default,
                YearAsNumber = int.MaxValue,
            };

            public string? Location;
            public SongMetadata Metadata;
            public RBMetadata RBMetadata;
            public LoaderSettings Settings;
            public AvailableParts Parts;
            public RBCONDifficulties Difficulties;

            public int YearAsNumber;
            public ulong SongLength;
        }

        protected static (ScanResult Result, ScanNode Info) ProcessDTAs(string nodename, DTAEntry baseDTA, CONModification modification)
        {
            float[]? volumes = null;
            float[]? pans = null;
            float[]? cores = null;

            var info = ScanNode.Default;
            void ParseDTA(DTAEntry entry)
            {
                if (entry.Name != null) { info.Metadata.Name = entry.Name; }
                if (entry.Artist != null) { info.Metadata.Artist = entry.Artist; }
                if (entry.Album != null) { info.Metadata.Album = entry.Album; }
                if (entry.Charter != null) { info.Metadata.Charter = entry.Charter; }
                if (entry.Genre != null) { info.Metadata.Genre = entry.Genre; }
                if (entry.YearAsNumber != null)
                {
                    info.YearAsNumber = entry.YearAsNumber.Value;
                    info.Metadata.Year = info.YearAsNumber.ToString("D4");
                }
                if (entry.Source != null) { info.Metadata.Source = entry.Source; }
                if (entry.Playlist != null) { info.Metadata.Playlist = entry.Playlist; }
                if (entry.SongLength != null) { info.SongLength = entry.SongLength.Value; }
                if (entry.IsMaster != null) { info.Metadata.IsMaster = entry.IsMaster.Value; }
                if (entry.AlbumTrack != null) { info.Metadata.AlbumTrack = entry.AlbumTrack.Value; }
                if (entry.PreviewStart != null)
                {
                    info.Metadata.PreviewStart = entry.PreviewStart.Value;
                    info.Metadata.PreviewEnd = entry.PreviewEnd!.Value;
                }
                if (entry.HopoThreshold != null) { info.Settings.HopoThreshold = entry.HopoThreshold.Value; }
                if (entry.SongRating != null) { info.Metadata.SongRating = entry.SongRating.Value; }
                if (entry.VocalPercussionBank != null) { info.RBMetadata.VocalPercussionBank = entry.VocalPercussionBank; }
                if (entry.VocalGender != null) { info.RBMetadata.VocalGender = entry.VocalGender.Value; }
                if (entry.VocalSongScrollSpeed != null) { info.RBMetadata.VocalSongScrollSpeed = entry.VocalSongScrollSpeed.Value; }
                if (entry.VocalTonicNote != null) { info.RBMetadata.VocalTonicNote = entry.VocalTonicNote.Value; }
                if (entry.VideoVenues != null) { info.RBMetadata.VideoVenues = entry.VideoVenues; }
                if (entry.DrumBank != null) { info.RBMetadata.DrumBank = entry.DrumBank; }
                if (entry.SongID != null) { info.RBMetadata.SongID = entry.SongID; }
                if (entry.SongTonality != null) { info.RBMetadata.SongTonality = entry.SongTonality.Value; }
                if (entry.Soloes != null) { info.RBMetadata.Soloes = entry.Soloes; }
                if (entry.AnimTempo != null) { info.RBMetadata.AnimTempo = entry.AnimTempo.Value; }
                if (entry.TuningOffsetCents != null) { info.RBMetadata.TuningOffsetCents = entry.TuningOffsetCents.Value; }
                if (entry.RealGuitarTuning != null) { info.RBMetadata.RealGuitarTuning = entry.RealGuitarTuning; }
                if (entry.RealBassTuning != null) { info.RBMetadata.RealBassTuning = entry.RealBassTuning; }

                if (entry.Cores != null) { cores = entry.Cores; }
                if (entry.Volumes != null) { volumes = entry.Volumes; }
                if (entry.Pans != null) { pans = entry.Pans; }

                if (entry.Location != null) { info.Location = entry.Location; }

                if (entry.Indices != null)
                {
                    var crowd = info.RBMetadata.Indices.Crowd;
                    info.RBMetadata.Indices = entry.Indices.Value;
                    info.RBMetadata.Indices.Crowd = crowd;
                }

                if (entry.CrowdChannels != null) { info.RBMetadata.Indices.Crowd = entry.CrowdChannels; }

                if (entry.Difficulties.Band >= 0) { info.Difficulties.Band = entry.Difficulties.Band; }
                if (entry.Difficulties.FiveFretGuitar >= 0) {  info.Difficulties.FiveFretGuitar = entry.Difficulties.FiveFretGuitar; }
                if (entry.Difficulties.FiveFretBass >= 0) { info.Difficulties.FiveFretBass = entry.Difficulties.FiveFretBass; }
                if (entry.Difficulties.FiveFretRhythm >= 0) { info.Difficulties.FiveFretRhythm = entry.Difficulties.FiveFretRhythm; }
                if (entry.Difficulties.FiveFretCoop >= 0) { info.Difficulties.FiveFretCoop = entry.Difficulties.FiveFretCoop; }
                if (entry.Difficulties.Keys >= 0) { info.Difficulties.Keys = entry.Difficulties.Keys; }
                if (entry.Difficulties.FourLaneDrums >= 0) { info.Difficulties.FourLaneDrums = entry.Difficulties.FourLaneDrums; }
                if (entry.Difficulties.ProDrums >= 0) { info.Difficulties.ProDrums = entry.Difficulties.ProDrums; }
                if (entry.Difficulties.ProGuitar >= 0) { info.Difficulties.ProGuitar = entry.Difficulties.ProGuitar; }
                if (entry.Difficulties.ProBass >= 0) { info.Difficulties.ProBass = entry.Difficulties.ProBass; }
                if (entry.Difficulties.ProKeys >= 0) { info.Difficulties.ProKeys = entry.Difficulties.ProKeys; }
                if (entry.Difficulties.LeadVocals >= 0) { info.Difficulties.LeadVocals = entry.Difficulties.LeadVocals; }
                if (entry.Difficulties.HarmonyVocals >= 0) { info.Difficulties.HarmonyVocals = entry.Difficulties.HarmonyVocals; }
            }

            ParseDTA(baseDTA);
            if (modification.UpdateDTA != null)
            {
                ParseDTA(modification.UpdateDTA);
            }

            if (modification.UpgradeDTA != null)
            {
                ParseDTA(modification.UpgradeDTA);
            }

            if (info.Metadata.Name.Length == 0)
            {
                return (ScanResult.NoName, info);
            }

            if (info.Location == null || pans == null || volumes == null || cores == null)
            {
                return (ScanResult.DTAError, info);
            }

            unsafe
            {
                var usedIndices = stackalloc bool[pans.Length];
                float[] CalculateStemValues(int[] indices)
                {
                    float[] values = new float[2 * indices.Length];
                    for (int i = 0; i < indices.Length; i++)
                    {
                        float theta = (pans[indices[i]] + 1) * ((float) Math.PI / 4);
                        float volRatio = (float) Math.Pow(10, volumes[indices[i]] / 20);
                        values[2 * i] = volRatio * (float) Math.Cos(theta);
                        values[2 * i + 1] = volRatio * (float) Math.Sin(theta);
                        usedIndices[indices[i]] = true;
                    }
                    return values;
                }

                if (info.RBMetadata.Indices.Drums.Length > 0)
                {
                    info.RBMetadata.Panning.Drums = CalculateStemValues(info.RBMetadata.Indices.Drums);
                }

                if (info.RBMetadata.Indices.Bass.Length > 0)
                {
                    info.RBMetadata.Panning.Bass = CalculateStemValues(info.RBMetadata.Indices.Bass);
                }

                if (info.RBMetadata.Indices.Guitar.Length > 0)
                {
                    info.RBMetadata.Panning.Guitar = CalculateStemValues(info.RBMetadata.Indices.Guitar);
                }

                if (info.RBMetadata.Indices.Keys.Length > 0)
                {
                    info.RBMetadata.Panning.Keys = CalculateStemValues(info.RBMetadata.Indices.Keys);
                }

                if (info.RBMetadata.Indices.Vocals.Length > 0)
                {
                    info.RBMetadata.Panning.Vocals = CalculateStemValues(info.RBMetadata.Indices.Vocals);
                }

                if (info.RBMetadata.Indices.Crowd.Length > 0)
                {
                    info.RBMetadata.Panning.Crowd = CalculateStemValues(info.RBMetadata.Indices.Crowd);
                }

                var leftover = new List<int>(pans.Length);
                for (int i = 0; i < pans.Length; i++)
                {
                    if (!usedIndices[i])
                    {
                        leftover.Add(i);
                    }
                }

                if (leftover.Count > 0)
                {
                    info.RBMetadata.Indices.Track = leftover.ToArray();
                    info.RBMetadata.Panning.Track = CalculateStemValues(info.RBMetadata.Indices.Track);
                }
            }

            if (info.Difficulties.FourLaneDrums > -1)
            {
                SetRank(ref info.Parts.FourLaneDrums.Intensity, info.Difficulties.FourLaneDrums, DrumDiffMap);
                if (info.Parts.ProDrums.Intensity == -1)
                {
                    info.Parts.ProDrums.Intensity = info.Parts.FourLaneDrums.Intensity;
                }
            }
            if (info.Difficulties.FiveFretGuitar > -1)
            {
                SetRank(ref info.Parts.FiveFretGuitar.Intensity, info.Difficulties.FiveFretGuitar, GuitarDiffMap);
                if (info.Parts.ProGuitar_17Fret.Intensity == -1)
                {
                    info.Parts.ProGuitar_22Fret.Intensity = info.Parts.ProGuitar_17Fret.Intensity = info.Parts.FiveFretGuitar.Intensity;
                }
            }
            if (info.Difficulties.FiveFretBass > -1)
            {
                SetRank(ref info.Parts.FiveFretBass.Intensity, info.Difficulties.FiveFretBass, GuitarDiffMap);
                if (info.Parts.ProBass_17Fret.Intensity == -1)
                {
                    info.Parts.ProBass_22Fret.Intensity = info.Parts.ProBass_17Fret.Intensity = info.Parts.FiveFretGuitar.Intensity;
                }
            }
            if (info.Difficulties.LeadVocals > -1)
            {
                SetRank(ref info.Parts.LeadVocals.Intensity, info.Difficulties.LeadVocals, GuitarDiffMap);
                if (info.Parts.HarmonyVocals.Intensity == -1)
                {
                    info.Parts.HarmonyVocals.Intensity = info.Parts.LeadVocals.Intensity;
                }
            }
            if (info.Difficulties.Keys > -1)
            {
                SetRank(ref info.Parts.Keys.Intensity, info.Difficulties.Keys, GuitarDiffMap);
                if (info.Parts.ProKeys.Intensity == -1)
                {
                    info.Parts.ProKeys.Intensity = info.Parts.Keys.Intensity;
                }
            }
            if (info.Difficulties.ProGuitar > -1)
            {
                SetRank(ref info.Parts.ProGuitar_17Fret.Intensity, info.Difficulties.ProGuitar, RealGuitarDiffMap);
                info.Parts.ProGuitar_22Fret.Intensity = info.Parts.ProGuitar_17Fret.Intensity;
                if (info.Parts.FiveFretGuitar.Intensity == -1)
                {
                    info.Parts.FiveFretGuitar.Intensity = info.Parts.ProGuitar_17Fret.Intensity;
                }
            }
            if (info.Difficulties.ProBass > -1)
            {
                SetRank(ref info.Parts.ProBass_17Fret.Intensity, info.Difficulties.ProBass, RealGuitarDiffMap);
                info.Parts.ProBass_22Fret.Intensity = info.Parts.ProBass_17Fret.Intensity;
                if (info.Parts.FiveFretBass.Intensity == -1)
                {
                    info.Parts.FiveFretBass.Intensity = info.Parts.ProBass_17Fret.Intensity;
                }
            }
            if (info.Difficulties.ProKeys > -1)
            {
                SetRank(ref info.Parts.ProKeys.Intensity, info.Difficulties.ProKeys, RealKeysDiffMap);
                if (info.Parts.Keys.Intensity == -1)
                {
                    info.Parts.Keys.Intensity = info.Parts.ProKeys.Intensity;
                }
            }
            if (info.Difficulties.ProDrums > -1)
            {
                SetRank(ref info.Parts.ProDrums.Intensity, info.Difficulties.ProDrums, DrumDiffMap);
                if (info.Parts.FourLaneDrums.Intensity == -1)
                {
                    info.Parts.FourLaneDrums.Intensity = info.Parts.ProDrums.Intensity;
                }
            }
            if (info.Difficulties.HarmonyVocals > -1)
            {
                SetRank(ref info.Parts.HarmonyVocals.Intensity, info.Difficulties.HarmonyVocals, DrumDiffMap);
                if (info.Parts.LeadVocals.Intensity == -1)
                {
                    info.Parts.LeadVocals.Intensity = info.Parts.HarmonyVocals.Intensity;
                }
            }
            if (info.Difficulties.Band > -1)
            {
                SetRank(ref info.Parts.BandDifficulty.Intensity, info.Difficulties.Band, BandDiffMap);
                info.Parts.BandDifficulty.SubTracks = 1;
            }
            return (ScanResult.Success, info);
        }

        protected static (ScanResult Result, HashWrapper Hash) ParseRBCONMidi(in FixedArray<byte> mainMidi, CONModification modification, ref ScanNode info)
        {
            try
            {
                DrumPreparseHandler drumTracker = new()
                {
                    Type = DrumsType.ProDrums
                };

                using var updateMidi = modification.Midi.HasValue ? FixedArray<byte>.Load(modification.Midi.Value.FullName) : FixedArray<byte>.Null;
                using var upgradeMidi = modification.UpgradeNode != null ? modification.UpgradeNode.LoadUpgradeMidi() : FixedArray<byte>.Null;
                if (modification.UpgradeNode != null && !upgradeMidi.IsAllocated)
                {
                    throw new FileNotFoundException("Upgrade midi not located");
                }

                long bufLength = mainMidi.Length;
                if (updateMidi.IsAllocated)
                {
                    switch (ParseMidi(in updateMidi, drumTracker, ref info.Parts).Result)
                    {
                        case ScanResult.InvalidResolution: return (ScanResult.InvalidResolution_Update, default);
                        case ScanResult.MultipleMidiTrackNames: return (ScanResult.MultipleMidiTrackNames_Update, default);
                    }
                    bufLength += updateMidi.Length;
                }

                if (upgradeMidi.IsAllocated)
                {
                    switch (ParseMidi(in upgradeMidi, drumTracker, ref info.Parts).Result)
                    {
                        case ScanResult.InvalidResolution: return (ScanResult.InvalidResolution_Upgrade, default);
                        case ScanResult.MultipleMidiTrackNames: return (ScanResult.MultipleMidiTrackNames_Upgrade, default);
                    }
                    bufLength += upgradeMidi.Length;
                }

                var (result, resolution) = ParseMidi(in mainMidi, drumTracker, ref info.Parts);
                if (result != ScanResult.Success)
                {
                    return (result, default);
                }

                SetDrums(ref info.Parts, drumTracker);
                if (!CheckScanValidity(in info.Parts))
                {
                    return (ScanResult.NoNotes, default);
                }

                info.Settings.SustainCutoffThreshold = resolution / 3;
                if (info.Settings.HopoThreshold == -1)
                {
                    info.Settings.HopoThreshold = info.Settings.SustainCutoffThreshold;
                }

                using var buffer = FixedArray<byte>.Alloc(bufLength);
                unsafe
                {
                    System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr, mainMidi.Ptr, (uint) mainMidi.Length);

                    long offset = mainMidi.Length;
                    if (updateMidi.IsAllocated)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, updateMidi.Ptr, (uint) updateMidi.Length);
                        offset += updateMidi.Length;
                    }

                    if (upgradeMidi.IsAllocated)
                    {
                        System.Runtime.CompilerServices.Unsafe.CopyBlock(buffer.Ptr + offset, upgradeMidi.Ptr, (uint) upgradeMidi.Length);
                    }
                }
                return (ScanResult.Success, HashWrapper.Hash(buffer.ReadOnlySpan));
            }
            catch (Exception ex)
            {
                YargLogger.LogException(ex);
                return (ScanResult.PossibleCorruption, default);
            }
        }

        protected static Stream? LoadUpdateMoggStream(in AbridgedFileInfo? info)
        {
            if (info == null)
            {
                return null;
            }

            var mogg = info.Value;
            if (!File.Exists(mogg.FullName))
            {
                return null;
            }

            if (mogg.FullName.EndsWith(".yarg_mogg"))
            {
                return new YargMoggReadStream(mogg.FullName);
            }
            return new FileStream(mogg.FullName, FileMode.Open, FileAccess.Read);
        }

        private static readonly int[] BandDiffMap = { 163, 215, 243, 267, 292, 345 };
        private static readonly int[] GuitarDiffMap = { 139, 176, 221, 267, 333, 409 };
        private static readonly int[] BassDiffMap = { 135, 181, 228, 293, 364, 436 };
        private static readonly int[] DrumDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] KeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] VocalsDiffMap = { 132, 175, 218, 279, 353, 427 };
        private static readonly int[] RealGuitarDiffMap = { 150, 205, 264, 323, 382, 442 };
        private static readonly int[] RealBassDiffMap = { 150, 208, 267, 325, 384, 442 };
        private static readonly int[] RealDrumsDiffMap = { 124, 151, 178, 242, 345, 448 };
        private static readonly int[] RealKeysDiffMap = { 153, 211, 269, 327, 385, 443 };
        private static readonly int[] HarmonyDiffMap = { 132, 175, 218, 279, 353, 427 };

        private static void SetRank(ref sbyte intensity, int rank, int[] values)
        {
            sbyte i = 0;
            while (i < 6 && values[i] <= rank)
            {
                ++i;
            }
            intensity = i;
        }

        private static void WriteUpdateInfo(in AbridgedFileInfo? info, MemoryStream stream)
        {
            stream.Write(info != null);
            if (info != null)
            {
                stream.Write(info.Value.FullName);
            }
        }
    }
}
