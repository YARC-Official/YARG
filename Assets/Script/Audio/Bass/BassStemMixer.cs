using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Core.Song;
using YARG.Helpers;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public sealed class BassStemMixer : StemMixer
    {
        #nullable enable
        private struct StemData
        {
            public readonly SongStem     Stem;
            public readonly float[,]?    VolumeMatrix;
            public readonly StreamHandle StreamHandles;
            public readonly StreamHandle ReverbHandles;
            public readonly double       PitchFxDelay;

            public StemData(SongStem stem, float[,]? volumeMatrix, StreamHandle streamHandles,
                StreamHandle reverbHandles, double pitchFxDelay)
            {
                Stem = stem;
                VolumeMatrix = volumeMatrix;
                StreamHandles = streamHandles;
                ReverbHandles = reverbHandles;
                PitchFxDelay = pitchFxDelay;
            }
        }
        #nullable disable

        private const    float WHAMMY_SYNC_INTERVAL_SECONDS = 1f;
        private const    float MIN_PLAYBACK_SPEED            = 0.05f;
        private const    float MAX_PLAYBACK_SPEED            = 51f;

        private static bool IsWhammyEnabled => SettingsManager.Settings.UseWhammyFx.Value;
        private        bool IsPlaying       => Bass.ChannelIsActive(_outputMixerHandle) == PlaybackState.Playing;

        private readonly int                         _mixerHandle;
        private readonly List<int>                   _sourceHandles = new();
        private readonly int                         _tempoStreamHandle;
        private readonly int                         _outputMixerHandle;
        private readonly SongPositionTracker         _songPositionTracker;
        private readonly BufferedPlaybackTimeline    _playbackTimeline;
        private          bool                        _didSeek;
        private          int                         _songEndHandle;
        private          float                       _songSpeed = 1.0f;
        private          float                       _speed     = 1.0f;
        private          Timer                       _whammySyncTimer;
        private readonly List<StemData>              _stemDatas       = new();
        private readonly HashSet<BassOneShotChannel> _oneShotChannels = new();
        private          int                         _longestHandle;

        private readonly BassNormalizer _normalizer = new();
        private          bool           _shouldNormalize;
        private          int            _gainDspHandle;
        private          float          _gain = 1.0f;

        public override event Action SongEnd
        {
            add
            {
                if (_songEndHandle == 0)
                {
                    void sync(int _, int __, int ___, IntPtr _____)
                    {
                        // Prevent potential race conditions by caching the value as a local
                        var end = _songEnd;
                        if (end != null)
                        {
                            UnityMainThreadCallback.QueueEvent(end.Invoke);
                        }
                    }
                    _songEndHandle = BassMix.ChannelSetSync(_longestHandle, SyncFlags.End, 0, sync);
                }

                _songEnd += value;
            }
            remove
            {
                _songEnd -= value;
            }
        }

#nullable enable
        internal BassStemMixer(string name, BassAudioManager manager, float speed, double volume, int handle,
            bool clampStemVolume, bool normalize, OutputChannel? outputChannel)
            : base(name, manager, clampStemVolume)
#nullable disable
        {
            _mixerHandle = handle;
            _tempoStreamHandle = BassFx.TempoCreate(handle,
                BassFlags.Decode | BassFlags.FxFreeSource);
            if (_tempoStreamHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create tempo stream: {0}", Bass.LastError);
                return;
            }

            var tempoInfo = Bass.ChannelGetInfo(_tempoStreamHandle);
            _outputMixerHandle = BassMix.CreateMixerStream(tempoInfo.Frequency, tempoInfo.Channels,
                BassFlags.Float | BassFlags.MixerNonStop);
            if (_outputMixerHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create output mixer: {0}", Bass.LastError);
                return;
            }

            if (!BassMix.MixerAddChannel(_outputMixerHandle, _tempoStreamHandle,
                BassFlags.MixerChanNoRampin))
            {
                YargLogger.LogFormatError("Failed to add tempo stream to output mixer: {0}", Bass.LastError);
                return;
            }

            _songPositionTracker = new SongPositionTracker(_tempoStreamHandle);
            _playbackTimeline = new BufferedPlaybackTimeline(speed);
            _shouldNormalize = normalize && SettingsManager.Settings.EnableNormalization.Value;
            if (_shouldNormalize)
            {
                AddGainDSP();
            }

            _whammySyncTimer = new Timer();
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(volume);
            SetPlaybackSpeed_Internal(speed, 0f, true);
            _BufferSetter(SettingsManager.Settings.PlaybackBufferLength.Value);
        }

        private void AddGainDSP()
        {
            _gainDspHandle = Bass.ChannelSetDSP(_mixerHandle, (handle, channel, buffer, length, user) =>
            {
                BassHelpers.ApplyGain(_gain, buffer, length);
            });

            if (_gainDspHandle == 0)
            {
                YargLogger.LogFormatError("Failed to add gain DSP: {0}!", Bass.LastError);
            }
        }


        protected override int Play_Internal()
        {
            if (_shouldNormalize)
            {
                _gain = _normalizer.Gain;
                _normalizer.OnGainAdjusted -= OnGainAdjusted;
                _normalizer.OnGainAdjusted += OnGainAdjusted;
            }

            if (!IsPlaying)
            {
                // Prime the stream after a seek, before starting playback. BASS documents this order
                // as the way to avoid initial decode/buffer-fill delay at ChannelPlay.
                Bass.ChannelUpdate(_outputMixerHandle, 0);

                // Restart flushes the output mixer's playback buffer after a seek. The tempo source
                // position is reset separately by SetPosition_Internal.
                bool playSucceeded = Bass.ChannelPlay(_outputMixerHandle, Restart: _didSeek);
                int playError = playSucceeded ? 0 : (int) Bass.LastError;

                if (!playSucceeded)
                {
                    return playError;
                }

                // Start control-rate tracking after ChannelPlay returns so mixer startup work is not
                // counted as song progress.
                _playbackTimeline.Play(_songPositionTracker.GetSongPosition());
                _didSeek = false;
            }

            if (IsWhammyEnabled)
            {
                _whammySyncTimer.Start(WHAMMY_SYNC_INTERVAL_SECONDS, SyncWhammyDrift);
            }

            return 0;
        }

        /// <summary>.
        /// The BASS PitchShift effect causes the stem playback to drift over time.
        /// It was discovered that we can correct the drift by setting the whammy pitch
        /// to 0% when no pitch shift is applied.
        /// </summary>
        private void SyncWhammyDrift()
        {
            foreach (var channel in Channels)
            {
                if (Mathf.Approximately(channel.GetWhammyPitch(), 1.0f))
                {
                    channel.SetWhammyPitch(percent: 0.0f);
                }
            }
        }

        private void OnGainAdjusted(float adjustedGain)
        {
            _gain = adjustedGain;
        }

        protected override void FadeIn_Internal(double maxVolume, double duration)
        {
            float scaled = (float) BassAudioManager.ExponentialVolume(maxVolume);
            Bass.ChannelSlideAttribute(_outputMixerHandle, ChannelAttribute.Volume, scaled, (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        protected override void FadeOut_Internal(double duration)
        {
            Bass.ChannelSlideAttribute(_outputMixerHandle, ChannelAttribute.Volume, 0, (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        protected override int Pause_Internal()
        {
            if (!IsPlaying)
            {
                _playbackTimeline.Pause();
                return 0;
            }

            if (!Bass.ChannelPause(_outputMixerHandle))
            {
                return (int) Bass.LastError;
            }

            _playbackTimeline.Pause();

            return 0;
        }

        protected override double GetPosition_Internal()
        {
            return _songPositionTracker.GetSongPosition();
        }

        /// <summary>
        /// Samples BASS playback once, then derives heard and predictive control positions from that
        /// same sample. BASSmix already compensates this position for mixer playback buffering.
        /// </summary>
        protected override SyncPosition GetSyncPosition_Internal()
        {
            double bassPosition = _songPositionTracker.GetSongPosition();
            return _playbackTimeline.GetSyncPosition(bassPosition);
        }

        protected override double GetControlPosition_Internal()
        {
            return GetSyncPosition_Internal().Control;
        }

        protected override double GetTempoStreamLatency_Internal()
        {
            return BassLatencyProvider.GetTempoStreamLatency(_outputMixerHandle);
        }

        // The total delay between playback command and when audio is heard
        public double GetPlaybackStartOffset()
        {
            return _playbackTimeline.OutputLatency + _songPositionTracker.AlignmentDelay;
        }

        protected override double GetVolume_Internal()
        {
            if (!Bass.ChannelGetAttribute(_outputMixerHandle, ChannelAttribute.Volume, out float volume))
            {
                YargLogger.LogFormatError("Failed to get volume: {0}", Bass.LastError);
            }
            return BassAudioManager.LogarithmicVolume(volume);
        }

        protected override void SetPosition_Internal(double position)
        {
            var wasPlaying = IsPlaying;
            Pause_Internal();

            double playbackOffset = GetPlaybackStartOffset() * _songSpeed;
            double preparedPosition = position + playbackOffset;
            double seekPosition = Math.Clamp(preparedPosition, 0, _length);
            double playbackDelay = Math.Max(0, -preparedPosition);

            RemoveChannelsFromMixer();
            if (AddChannelsToMixer(_stemDatas, playbackDelay, out double alignmentDelay))
            {
                foreach (var channel in _channels)
                {
                    channel.SetPosition(seekPosition);
                }
                _didSeek = true;
                _songPositionTracker.Reset(seekPosition, alignmentDelay, playbackDelay);
                if (!BassMix.ChannelSetPosition(_tempoStreamHandle, 0, PositionFlags.Bytes))
                {
                    YargLogger.LogFormatError("Failed to reset tempo stream position: {0}!", Bass.LastError);
                }

                // Reset the playback mixer before sampling the prepared position below. Resetting its
                // source does not reliably clear BASSmix's buffered source-position history, so without
                // this the old pre-pause position can be added to the new song start on resume.
                if (!Bass.ChannelSetPosition(_outputMixerHandle, 0, PositionFlags.Bytes))
                {
                    YargLogger.LogFormatError("Failed to reset output mixer position: {0}!", Bass.LastError);
                }

                _playbackTimeline.ResetAfterSeek(_songPositionTracker.GetSongPosition(), position);
                foreach (var channel in _oneShotChannels)
                {
                    channel.ResetAfterSeek();
                }
            }

            if (wasPlaying)
            {
                Play_Internal();
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            volume = BassAudioManager.ExponentialVolume(volume);
            if (!Bass.ChannelSetAttribute(_outputMixerHandle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set output mixer volume: {0}", Bass.LastError);
            }
        }

        protected override int GetFFTData_Internal(float[] buffer, int fftSize, bool complex)
        {
            int flags = 0;
            switch (1 << fftSize)
            {
                case 256:
                    flags |= (int) DataFlags.FFT256;
                    break;
                case 512:
                    flags |= (int) DataFlags.FFT512;
                    break;
                case 1024:
                    flags |= (int) DataFlags.FFT1024;
                    break;
                case 2048:
                    flags |= (int) DataFlags.FFT2048;
                    break;
                case 4096:
                    flags |= (int) DataFlags.FFT4096;
                    break;
                default:
                    return -1;
            }

            if (complex)
            {
                flags |= (int) DataFlags.FFTComplex;
            }

            int data = Bass.ChannelGetData(_outputMixerHandle, buffer, flags);
            if (data < 0)
            {
                return (int) Bass.LastError;
            }
            return data;
        }

        protected override int GetSampleData_Internal(float[] buffer)
        {
            int data = Bass.ChannelGetData(_outputMixerHandle, buffer, (buffer.Length * 4) | (int) (DataFlags.Float));
            if (data < 0)
            {
                return (int) Bass.LastError;
            }
            return data;
        }

        protected override int GetLevel_Internal(float[] level)
        {
            bool status = Bass.ChannelGetLevel(_outputMixerHandle, level, 0.2f, LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS);
            if (!status)
            {
                return (int) Bass.LastError;
            }

            return (int) Errors.OK;
        }

        protected override void SetPlaybackSpeed_Internal(float songSpeed, float syncAdjustment, bool shiftPitch)
        {
            // SongRunner clamps requested song speed, but the temporary synchronization adjustment can
            // push the effective speed outside BASS_FX's supported 5%-5100% tempo range.
            float effectiveSpeed = Math.Clamp(
                songSpeed + syncAdjustment,
                MIN_PLAYBACK_SPEED,
                MAX_PLAYBACK_SPEED
            );

            // Model the speed BASS actually receives. This can differ from the requested adjustment
            // when the effective speed reaches one of the limits above.
            float appliedAdjustment = effectiveSpeed - songSpeed;
            _songSpeed = songSpeed;

            // Exact comparison is intentional. If BASS receives a new float value, the playback model
            // must record the same value; an approximate comparison could let the two drift apart.
            bool speedChanged = _speed != effectiveSpeed;
            if (speedChanged)
            {
                _speed = effectiveSpeed;
                BassAudioManager.SetSpeed(effectiveSpeed, _tempoStreamHandle, shiftPitch);
            }

            double tempoLatency = BassLatencyProvider.GetTempoStreamLatency(_outputMixerHandle);
            _playbackTimeline.SetSpeed(songSpeed, appliedAdjustment, tempoLatency);
            if (!speedChanged)
            {
                return;
            }
            foreach (var channel in _oneShotChannels)
            {
                channel.ResetAfterSpeedChange();
            }
        }

        protected override void SetOutputLatency_Internal(double latency)
        {
            _playbackTimeline.SetOutputLatency(latency);
        }

        protected override bool AddChannels_Internal(Stream stream, params StemInfo[] stemInfos)
        {
            if (_shouldNormalize)
            {
                if (!_normalizer.AddStream(stream, stemInfos))
                {
                    YargLogger.LogError("Failed to add stream to normalizer. Disabling normalization.");
                    _shouldNormalize = false;
                }
            }

            if (!BassAudioManager.CreateSourceStream(stream, out int sourceStream))
            {
                YargLogger.LogFormatError("Failed to load stem source stream: {0}!", Bass.LastError);
                return false;
            }

            _sourceHandles.Add(sourceStream);

            if (!BuildStemData(sourceStream, stemInfos, out var stemDatas))
            {
                return false;
            }

            _stemDatas.AddRange(stemDatas);

            // Every stem is padded to match the largest pitch-effect delay in the mixer. A new stem can
            // increase that delay, so rebuild all mixer channels to keep every stem aligned. Rebuilding
            // also prevents the existing streams from being added a second time below.
            RemoveChannelsFromMixer();
            if (!AddChannelsToMixer(_stemDatas, 0, out double delay))
            {
                _stemDatas.RemoveAll(stemDatas.Contains);
                return false;
            }
            _songPositionTracker.SetAlignmentDelay(delay);

            foreach (var stemStreamData in stemDatas)
            {
                CreateChannel(
                    stem: stemStreamData.Stem,
                    sourceHandle: sourceStream,
                    streamHandles: stemStreamData.StreamHandles,
                    reverbHandles: stemStreamData.ReverbHandles
                );
            }

            return true;
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            BassHelpers.UpdateOutputChannels(_outputMixerHandle, channel);
        }

        protected override void SetOutputDevice_Internal(OutputDevice device)
        {
            if (device is not BassOutputDevice bassDevice)
            {
                return;
            }

            foreach (StemData stemData in _stemDatas)
            {
                if (!Bass.ChannelSetDevice(stemData.ReverbHandles.Stream, bassDevice.DeviceId))
                {
                    YargLogger.LogFormatError("Failed to change device for reverb handle: {0}", Bass.LastError);
                }

                if (!Bass.ChannelSetDevice(stemData.StreamHandles.Stream, bassDevice.DeviceId))
                {
                    YargLogger.LogFormatError("Failed to change device for stream handle: {0}", Bass.LastError);
                }
            }

            foreach (int handle in _sourceHandles)
            {
                if (!Bass.ChannelSetDevice(handle, bassDevice.DeviceId))
                {
                    YargLogger.LogFormatError("Failed to change device for source handle: {0}", Bass.LastError);
                }
            }

            if (_mixerHandle != 0 && !Bass.ChannelSetDevice(_mixerHandle, bassDevice.DeviceId))
            {
                YargLogger.LogFormatError("Failed to change device for mixer handle: {0}", Bass.LastError);
            }

            if (_tempoStreamHandle != 0 && !Bass.ChannelSetDevice(_tempoStreamHandle, bassDevice.DeviceId))
            {
                YargLogger.LogFormatError("Failed to change device for tempo stream handle: {0}", Bass.LastError);
            }

            if (_outputMixerHandle != 0 && !Bass.ChannelSetDevice(_outputMixerHandle, bassDevice.DeviceId))
            {
                YargLogger.LogFormatError("Failed to change device for output mixer handle: {0}", Bass.LastError);
            }
        }

        private void RemoveChannelsFromMixer()
        {
            foreach (int channel in BassMix.MixerGetChannels(_mixerHandle))
            {
                if (!BassMix.MixerRemoveChannel(channel))
                {
                    YargLogger.LogDebug("Failed to remove channel from mixer");
                }
            }
            foreach (var channel in _oneShotChannels)
            {
                channel.PrepareForSeek();
            }
        }

        private static bool BuildStemData(int sourceStream, IEnumerable<StemInfo> stemInfos,
            out List<StemData> stemDatas)
        {
            stemDatas = new List<StemData>();

            foreach (var group in stemInfos.GroupBy(info => info.Stem))
            {
                var stem = group.Key;
                var allIndices = group
                    .Where(info => info.Indices != null)
                    .SelectMany(info => info.Indices)
                    .ToArray();

                var handles = BassAudioManager.CreateSplitStreams(sourceStream, allIndices);
                if (handles == null)
                {
                    YargLogger.LogFormatError("Failed to load stem {0}: {1}!", stem, Bass.LastError);
                    continue;
                }

                var (streamHandle, reverbHandle) = handles.Value;
                double pitchFxDelay = 0;
                if (GlobalAudioHandler.UseWhammyFx && AudioHelpers.PitchBendAllowedStems.Contains(stem))
                {
                    if (!Bass.ChannelGetAttribute(streamHandle.Stream, ChannelAttribute.Frequency,
                        out float frequency))
                    {
                        YargLogger.LogFormatError(
                            "Failed to get frequency for stem {0}: {1}!", stem, Bass.LastError
                        );
                        return false;
                    }

                    // BASS_FX pitch shift buffers one full FFT frame. Use source stream frequency:
                    // low-rate stems otherwise receive only half required compensation and drift
                    // ahead of stems without pitch FX.
                    pitchFxDelay = GlobalAudioHandler.WHAMMY_FFT_DEFAULT / frequency;
                }

                float[,] volumeMatrix = BuildVolumeMatrix(group, allIndices.Length);
                stemDatas.Add(new StemData(stem, volumeMatrix, streamHandle, reverbHandle, pitchFxDelay));
            }

            if (stemDatas.Count > 0)
            {
                return true;
            }

            YargLogger.LogError("Failed to load any stems!");
            return false;
        }

        private bool AddChannelsToMixer(IEnumerable<StemData> stemStreamDataList, double playbackDelay,
            out double alignmentDelay)
        {
            var stemData = stemStreamDataList.ToArray();

            // Align every stem with the largest pitch fx latency.  Latencies per stem can differ due to sample rate
            alignmentDelay = stemData.Max(data => data.PitchFxDelay);

            foreach (var data in stemData)
            {
                var stem = data.Stem;
                var streamHandles = data.StreamHandles;
                var reverbHandles = data.ReverbHandles;
                var volumeMatrix = data.VolumeMatrix;

                // Each stem already incurs its own processing delay. Add the difference from the maximum so every
                // stem has the same total delay.
                double addedDelay = playbackDelay + alignmentDelay - data.PitchFxDelay;
                long delayBytes = Bass.ChannelSeconds2Bytes(_mixerHandle, addedDelay);

                var flags = volumeMatrix != null ? BassFlags.MixerChanMatrix : BassFlags.Default;
                if (!BassMix.MixerAddChannel(_mixerHandle, streamHandles.Stream, flags, delayBytes, 0) ||
                    !BassMix.MixerAddChannel(_mixerHandle, reverbHandles.Stream, flags, delayBytes, 0))
                {
                    YargLogger.LogFormatError("Failed to add channel {0} to mixer: {1}!", stem, Bass.LastError);
                    return false;
                }

                if (volumeMatrix == null)
                {
                    continue;
                }

                if (!BassMix.ChannelSetMatrix(streamHandles.Stream, volumeMatrix) || !BassMix.ChannelSetMatrix(reverbHandles.Stream, volumeMatrix))
                {
                    YargLogger.LogFormatError("Failed to set {0} matrices: {1}!", stem, Bass.LastError);
                    return false;
                }
            }
            return true;
        }



        internal static float[,] BuildVolumeMatrix(StemInfo info)
        {
            if (info.Indices == null || info.Panning == null)
            {
                return null;
            }
            return BuildVolumeMatrix(new[] { info }, info.Indices.Length);
        }

#nullable enable
        private static float[,]? BuildVolumeMatrix(IEnumerable<StemInfo> infos, int totalChannels)
#nullable disable
        {
            if (totalChannels == 0)
            {
                return null;
            }

            float[,] volumeMatrix = new float[2, totalChannels];
            const int leftPan = 0;
            const int rightPan = 1;

            int channelIndex = 0;
            foreach (var info in infos)
            {
                var panning = info.Panning;
                for (int i = 0; i < info.Indices.Length; ++i)
                {
                    volumeMatrix[leftPan, channelIndex] = panning[2 * i];
                    volumeMatrix[rightPan, channelIndex] = panning[2 * i + 1];
                    channelIndex++;
                }
            }
            return volumeMatrix;
        }

        protected override bool RemoveChannel_Internal(SongStem stemToRemove)
        {
            int index = _channels.FindIndex(channel => channel.Stem == stemToRemove);
            if (index == -1)
            {
                return false;
            }
            _channels[index].Dispose();
            _channels.RemoveAt(index);
            _stemDatas.RemoveAll(stem => stem.Stem == stemToRemove);
            UpdateThreading();
            return true;
        }

        protected override void SetBufferLength_Internal(int length)
        {
            _BufferSetter(length);
        }

        private void _BufferSetter(int length)
        {
            // 0 disables buffering. Positive values must meet BASS minimum buffer requirements.
            length = BassHelpers.ClampPlaybackBufferLength(length);
            float lengthInSeconds = length / 1000f;
            if (!Bass.ChannelSetAttribute(_outputMixerHandle, ChannelAttribute.Buffer, lengthInSeconds))
            {
                YargLogger.LogFormatError("Failed to set playback buffer: {0}!", Bass.LastError);
            }
        }

        protected override void DisposeManagedResources()
        {
            _whammySyncTimer.Stop();
            _whammySyncTimer = null;
            _stemDatas.Clear();
            if (_channels.Count == 0)
            {
                return;
            }
            if (_gainDspHandle != 0)
            {
                Bass.ChannelRemoveDSP(_mixerHandle, _gainDspHandle);
            }


            _normalizer.OnGainAdjusted -= OnGainAdjusted;
            _normalizer.Dispose();

            foreach (var channel in Channels)
            {
                channel.Dispose();
            }

            foreach (var sourceHandle in _sourceHandles)
            {
                if (!Bass.StreamFree(sourceHandle))
                {
                    YargLogger.LogFormatError("Failed to free source stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                }
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            // One-shot decoders are independent streams and must be freed before their mixer.
            foreach (var channel in _oneShotChannels.ToArray())
            {
                channel.Dispose();
            }
            _oneShotChannels.Clear();

            if (_outputMixerHandle != 0)
            {
                if (!Bass.StreamFree(_outputMixerHandle))
                {
                    YargLogger.LogFormatError("Failed to free output mixer stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                }
            }

            // Tempo stream owns and frees its source mixer via BassFlags.FxFreeSource.
            if (_tempoStreamHandle != 0)
            {
                if (!Bass.StreamFree(_tempoStreamHandle))
                {
                    YargLogger.LogFormatError("Failed to free tempo stream: {0}!", Bass.LastError);
                }
            }
        }

        private void CreateChannel(SongStem stem, int sourceHandle, StreamHandle streamHandles, StreamHandle reverbHandles)
        {
            var pitchparams = BassAudioManager.SetPitchParams(stem, _speed, streamHandles, reverbHandles);
            var stemchannel = new BassStemChannel(_manager, stem, _clampStemVolume, sourceHandle, pitchparams, streamHandles, reverbHandles);
            double length = BassAudioManager.GetLengthInSeconds(streamHandles.Stream);
            if (length > _length)
            {
                _longestHandle = streamHandles.Stream;
                _length = length;
            }
            _channels.Add(stemchannel);
            UpdateThreading();
        }

        private void UpdateThreading()
        {
            if (0 < _channels.Count && _channels.Count <= GlobalAudioHandler.MAX_THREADS)
            {
                // Mixer processing threads (for some reason this attribute is undocumented in ManagedBass?)
                // https://www.un4seen.com/forum/?topic=19491.msg136328#msg136328
                if (!Bass.ChannelSetAttribute(_mixerHandle, (ChannelAttribute) 86017, _channels.Count))
                {
                    YargLogger.LogFormatError("Failed to set mixer processing threads: {0}!", Bass.LastError);
                }
            }
        }

        public override OneShotChannel CreateOneShotChannel(int sampleStream,
            IReadOnlyList<double> scheduledPlays, double outputLeadTime = 0)
        {
            var channel = new BassOneShotChannel(
                _outputMixerHandle,
                _tempoStreamHandle,
                sampleStream,
                scheduledPlays,
                _songPositionTracker.GetSongPosition,
                () => _speed,
                outputLeadTime
            );
            channel.Disposed += OnOneShotDisposed;
            _oneShotChannels.Add(channel);
            return channel;
        }

        private void OnOneShotDisposed(BassOneShotChannel channel)
        {
            _oneShotChannels.Remove(channel);
        }

        /// <summary>
        /// Gets actual song position from tempo stream.
        /// <para>
        /// Calculated as: tempo stream position + last seek position - alignment delay.
        /// </para>
        /// <para>
        /// Tempo stream position advances continuously during playback and resets to zero after each seek.
        /// Last seek position is the position of the most recent seek in the song. Alignment delay is applied
        /// to all stems to keep them synchronized when using whammy FX and varies based on sample rate.
        /// </para>
        /// </summary>
        private sealed class SongPositionTracker
        {
            private readonly int    _tempoStreamHandle;
            private          double _songStart;
            private          double _playbackDelay;

            public  double AlignmentDelay { get; private set; }

            private double TotalDelay     => AlignmentDelay + _playbackDelay;

            public SongPositionTracker(int tempoStreamHandle)
            {
                _tempoStreamHandle = tempoStreamHandle;
            }

            /// <summary>
            /// Gets the current position in the song, in seconds.
            /// </summary>
            public double GetSongPosition()
            {
                double position = GetTempoStreamPosition();
                if (position < 0)
                {
                    return 0;
                }
                return position - TotalDelay + _songStart;
            }

            public double GetSongPosition(long tempoStreamPosition)
            {
                double position = Bass.ChannelBytes2Seconds(_tempoStreamHandle, tempoStreamPosition);
                return position - TotalDelay + _songStart;
            }
            /// <summary>
            /// Starts tracking from the requested song position after a seek
            /// </summary>
            public void Reset(double songStart, double alignmentDelay, double playbackDelay)
            {
                _songStart = songStart;
                AlignmentDelay = alignmentDelay;
                _playbackDelay = playbackDelay;
            }

            public void SetAlignmentDelay(double delay)
            {
                AlignmentDelay = delay;
            }

            private double GetTempoStreamPosition()
            {
                long positionBytes = BassMix.ChannelGetPosition(_tempoStreamHandle, PositionFlags.Bytes);
                if (positionBytes < 0)
                {
                    YargLogger.LogFormatError("Failed to get byte position: {0}!", Bass.LastError);
                    return -1;
                }

                double position = Bass.ChannelBytes2Seconds(_tempoStreamHandle, positionBytes);
                if (position < 0)
                {
                    YargLogger.LogFormatError("Failed to convert bytes to seconds: {0}!", Bass.LastError);
                    return -1;
                }

                return position;
            }
        }
    }

}
