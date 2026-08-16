#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedBass;
using YARG.Audio.BASS.Effects;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;

namespace YARG.Audio.BASS
{
    internal readonly struct StemAudioHandles
    {
        public readonly SongStem     Stem;
        public readonly int          SourceHandle;
        public readonly StreamHandle StreamHandles;
        public readonly StreamHandle ReverbHandles;

        public StemAudioHandles(SongStem stem, int sourceHandle, StreamHandle streamHandles,
            StreamHandle reverbHandles)
        {
            Stem = stem;
            SourceHandle = sourceHandle;
            StreamHandles = streamHandles;
            ReverbHandles = reverbHandles;
        }
    }

    /// <summary>
    ///     Builds and manages the audio processing chain for a song's stems: decoding audio files,
    ///     mixing stems, applying optional compression and loudness normalization, and hosting the tempo stream.
    /// </summary>
    internal sealed class BassStemPipeline : IDisposable
    {
        private readonly BassGainDsp?     _gainDsp;
        private readonly BassMixer        _mixer;
        private readonly BassNormalizer?  _normalizer;
        private readonly BassMixer        _reverbMixer;
        private readonly BassFreeverbDsp  _reverbDsp;
        private readonly List<StemData>   _stemDatas = new();
        private readonly BassTempoStream  _tempoStream;
        private          bool             _disposed;
        private          bool             _normalizationEnabled;

        private BassStemPipeline(BassMixer mixer, BassMixer reverbMixer, BassFreeverbDsp reverbDsp,
            BassTempoStream tempoStream,
            BassNormalizer? normalizer, BassGainDsp? gainDsp)
        {
            _mixer = mixer;
            _reverbMixer = reverbMixer;
            _reverbDsp = reverbDsp;
            _tempoStream = tempoStream;
            _normalizer = normalizer;
            _gainDsp = gainDsp;
            _normalizationEnabled = normalizer != null;
        }

        public int OutputHandle => _tempoStream.Handle;

        public static BassStemPipeline? Create(int sampleRate, int channelCount, BassFlags flags,
            bool withCompressor = true, bool withNormalization = false, int processingThreads = 0)
        {
            var mixer = BassMixer.Create(sampleRate, channelCount, flags, processingThreads);
            if (mixer == null)
            {
                return null;
            }

            if (withCompressor && BassHelpers.AddCompressorToChannel(mixer.Handle) == 0)
            {
                YargLogger.LogError("Failed to set up compressor for mixer stream!");
                mixer.Dispose();
                return null;
            }

            var reverbMixer = BassMixer.Create(sampleRate, channelCount, flags, processingThreads);
            if (reverbMixer == null)
            {
                mixer.Dispose();
                return null;
            }

            int reverbLowEq = BassHelpers.AddEqToChannel(reverbMixer.Handle, BassHelpers.LowEqParams);
            int reverbMidEq = BassHelpers.AddEqToChannel(reverbMixer.Handle, BassHelpers.MidEqParams);
            int reverbHighEq = BassHelpers.AddEqToChannel(reverbMixer.Handle, BassHelpers.HighEqParams);
            if (reverbLowEq == 0 || reverbMidEq == 0 || reverbHighEq == 0)
            {
                DisposeReverbBus(reverbMixer, null);
                mixer.Dispose();
                return null;
            }

            var reverbDsp = BassFreeverbDsp.Create(reverbMixer.Handle,
                dryMix: 0.0f,
                wetMix: 1.0f,
                roomSize: 0.8f,
                damp: 0.5f,
                width: 1.0f);
            if (reverbDsp == null)
            {
                DisposeReverbBus(reverbMixer, null);
                mixer.Dispose();
                return null;
            }

            if (!mixer.AddChannel(reverbMixer.Handle))
            {
                DisposeReverbBus(reverbMixer, reverbDsp);
                mixer.Dispose();
                return null;
            }

            BassTempoStream tempoStream;
            try
            {
                tempoStream = BassTempoStream.Create(mixer.Handle);
                tempoStream.Prime();
            }
            catch (BassX.BassOperationException exception)
            {
                YargLogger.LogError(exception.Message);
                mixer.RemoveChannel(reverbMixer.Handle);
                DisposeReverbBus(reverbMixer, reverbDsp);
                mixer.Dispose();
                return null;
            }

            BassGainDsp? gainDsp = null;
            BassNormalizer? normalizer = null;
            if (withNormalization)
            {
                gainDsp = BassGainDsp.Attach(mixer.Handle, BassNormalizer.INITIAL_GAIN);
                if (gainDsp != null)
                {
                    normalizer = new BassNormalizer(gain => gainDsp.SetGain(gain));
                }
            }

            return new BassStemPipeline(mixer, reverbMixer, reverbDsp, tempoStream, normalizer, gainDsp);
        }

        public bool AddStems(Stream stream, IEnumerable<StemMixer.StemInfo> stemInfos,
            out List<StemAudioHandles> createdStems, out double alignmentDelay)
        {
            createdStems = new List<StemAudioHandles>();
            alignmentDelay = 0;

            if (_disposed)
            {
                return false;
            }

            var stemInfoList = stemInfos as IReadOnlyCollection<StemMixer.StemInfo> ?? stemInfos.ToArray();
            if (_normalizationEnabled && !_normalizer!.AddStream(stream, stemInfoList.ToArray()))
            {
                YargLogger.LogError("Failed to add stream to normalizer. Disabling normalization.");
                StopNormalization();
            }

            var source = _mixer.CreateSource(stream);
            if (source == null)
            {
                return false;
            }

            if (!BuildStemData(source, stemInfoList, out var newStems))
            {
                source.Dispose();
                return false;
            }

            _stemDatas.AddRange(newStems);

            if (!RealignChannels(0, out alignmentDelay))
            {
                _stemDatas.RemoveAll(data => data.Source == source);
                source.Dispose();
                RealignChannels(0, out _);
                return false;
            }

            foreach (var data in newStems)
            {
                createdStems.Add(new StemAudioHandles(data.Stem, source.Handle, data.StreamHandles,
                    data.ReverbHandles));
            }

            UpdateThreading();
            return true;
        }

        public bool RealignChannels(double playbackDelay, out double alignmentDelay)
        {
            _mixer.RemoveAllChannels();
            _reverbMixer.RemoveAllChannels();

            alignmentDelay = 0;
            foreach (var data in _stemDatas)
            {
                if (data.PitchFxDelay > alignmentDelay)
                {
                    alignmentDelay = data.PitchFxDelay;
                }
            }

            var dryChannels = new List<BassMixerChannel>(_stemDatas.Count);
            var reverbChannels = new List<BassMixerChannel>(_stemDatas.Count);

            foreach (var data in _stemDatas)
            {
                double delay = playbackDelay + alignmentDelay - data.PitchFxDelay;
                dryChannels.Add(new BassMixerChannel(data.StreamHandles.Stream, data.VolumeMatrix, delay));
                reverbChannels.Add(new BassMixerChannel(data.ReverbHandles.Stream, data.VolumeMatrix, delay));
            }

            bool added = _reverbMixer.AddChannels(reverbChannels) && _mixer.AddChannels(dryChannels) &&
                _mixer.AddChannel(_reverbMixer.Handle);
            if (!added)
            {
                _mixer.RemoveAllChannels();
                _reverbMixer.RemoveAllChannels();
                return false;
            }

            _tempoStream.ResetPosition();
            _reverbDsp.RequestReset();
            return true;
        }

        public bool RemoveStem(SongStem stemToRemove)
        {
            bool removed = false;
            for (int i = _stemDatas.Count - 1; i >= 0; i--)
            {
                var data = _stemDatas[i];
                if (data.Stem == stemToRemove)
                {
                    _mixer.RemoveChannel(data.StreamHandles.Stream);
                    _reverbMixer.RemoveChannel(data.ReverbHandles.Stream);
                    data.Source.Release(data.StreamHandles);
                    data.Source.Release(data.ReverbHandles);
                    _stemDatas.RemoveAt(i);
                    removed = true;
                }
            }

            if (removed)
            {
                UpdateThreading();
            }

            return removed;
        }

        public void SetSpeed(float speed, bool shiftPitch) => _tempoStream.SetSpeed(speed, shiftPitch);

        public bool TryGetPositionSeconds(long positionBytes, out double seconds) =>
            _tempoStream.TryGetPositionSeconds(positionBytes, out seconds);

        public void SetDevice(int deviceId)
        {
            StopNormalization();
            _reverbMixer.SetDevice(deviceId);
            _mixer.SetDevice(deviceId);
            _tempoStream.SetDevice(deviceId);
        }

        public void StopNormalization()
        {
            _normalizationEnabled = false;
            _normalizer?.Dispose();
        }

        public void ApplyNormalizationGain()
        {
            if (_normalizer != null && _gainDsp != null)
            {
                _gainDsp.SetGain(_normalizer.Gain);
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stemDatas.Clear();
            _normalizer?.Dispose();
            _gainDsp?.Dispose();
            _tempoStream.Dispose();
            _mixer.RemoveChannel(_reverbMixer.Handle);
            _reverbMixer.RemoveAllChannels();
            DisposeReverbBus(_reverbMixer, _reverbDsp);
            _mixer.Dispose();
        }

        private static void DisposeReverbBus(BassMixer mixer, BassFreeverbDsp? reverbDsp)
        {
            reverbDsp?.Dispose();
            mixer.Dispose();
        }

        private static bool BuildStemData(BassMixerSource source, IEnumerable<StemMixer.StemInfo> stemInfos,
            out List<StemData> stemDatas)
        {
            stemDatas = new List<StemData>();

            foreach (var group in stemInfos.GroupBy(info => info.Stem))
            {
                var stem = group.Key;
                int[] indices = group.Where(info => info.Indices != null).SelectMany(info => info.Indices!).ToArray();

                var handles = source.CreateSplitPair(indices);
                if (handles == null)
                {
                    YargLogger.LogFormatError("Failed to load stem {0}: {1}!", stem, Bass.LastError);
                    continue;
                }

                var (streamHandle, reverbHandle) = handles.Value;
                if (!Bass.ChannelSlideAttribute(reverbHandle.Stream, ChannelAttribute.Volume, 0, 0))
                {
                    YargLogger.LogFormatError("Failed to initialize reverb volume for stem {0}: {1}!", stem,
                        Bass.LastError);
                    return false;
                }

                double pitchFxDelay = GetPitchDelay(stem, streamHandle);
                if (pitchFxDelay < 0)
                {
                    return false;
                }

                float[,]? volumeMatrix = BassHelpers.BuildVolumeMatrix(group, indices.Length);
                stemDatas.Add(new StemData(stem, source, volumeMatrix, streamHandle, reverbHandle, pitchFxDelay));
            }

            if (stemDatas.Count > 0)
            {
                return true;
            }

            YargLogger.LogError("Failed to load any stems!");
            return false;
        }

        private static double GetPitchDelay(SongStem stem, StreamHandle streamHandle)
        {
            if (!GlobalAudioHandler.UseWhammyFx || !AudioHelpers.PitchBendAllowedStems.Contains(stem))
            {
                return 0;
            }

            if (Bass.ChannelGetAttribute(streamHandle.Stream, ChannelAttribute.Frequency, out float frequency))
            {
                return GlobalAudioHandler.WHAMMY_FFT_DEFAULT / frequency;
            }

            YargLogger.LogFormatError("Failed to get frequency for channel {0}!", streamHandle.Stream);
            return -1;
        }

        private void UpdateThreading()
        {
            if (_stemDatas.Count == 0 || _stemDatas.Count > GlobalAudioHandler.MAX_THREADS)
            {
                return;
            }

            _reverbMixer.SetProcessingThreads(_stemDatas.Count);
            _mixer.SetProcessingThreads(_stemDatas.Count);
        }

        private readonly struct StemData
        {
            public readonly SongStem        Stem;
            public readonly BassMixerSource Source;
            public readonly float[,]?       VolumeMatrix;
            public readonly StreamHandle    StreamHandles;
            public readonly StreamHandle    ReverbHandles;
            public readonly double          PitchFxDelay;

            public StemData(SongStem stem, BassMixerSource source, float[,]? volumeMatrix,
                StreamHandle streamHandles, StreamHandle reverbHandles, double pitchFxDelay)
            {
                Stem = stem;
                Source = source;
                VolumeMatrix = volumeMatrix;
                StreamHandles = streamHandles;
                ReverbHandles = reverbHandles;
                PitchFxDelay = pitchFxDelay;
            }
        }
    }
}
