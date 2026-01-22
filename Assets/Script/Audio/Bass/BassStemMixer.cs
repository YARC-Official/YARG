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
            public SongStem     Stem;
            public float[,]?    VolumeMatrix;
            public StreamHandle StreamHandles;
            public StreamHandle ReverbHandles;

            public StemData(SongStem stem, float[,]? volumeMatrix, StreamHandle streamHandles, StreamHandle reverbHandles)
            {
                Stem = stem;
                VolumeMatrix = volumeMatrix;
                StreamHandles = streamHandles;
                ReverbHandles = reverbHandles;
            }
        }
        #nullable disable

        private const    float WHAMMY_SYNC_INTERVAL_SECONDS = 1f;

        private       bool  IsWhammyEnabled => SettingsManager.Settings.UseWhammyFx.Value;
        private bool IsPlaying => Bass.ChannelIsActive(_tempoStreamHandle) == PlaybackState.Playing;

        private readonly int            _mixerHandle;
        private readonly List<int>      _sourceHandles = new();
        private          int            _tempoStreamHandle;
        private          double         _positionOffset = 0.0;
        private          bool           _didSetPosition = false;
        private          int            _songEndHandle;
        private          float          _speed = 1.0f;
        private          Timer          _whammySyncTimer;
        private readonly List<StemData> _stemDatas = new();
        private          int            _longestHandle;

        private BassNormalizer _normalizer = new();
        private bool           _shouldNormalize;
        private int   _gainDspHandle;
        private float _gain = 1.0f;

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

        internal BassStemMixer(string name, BassAudioManager manager, float speed, double volume, int handle,
            bool clampStemVolume, bool normalize)
            : base(name, manager, clampStemVolume)
        {
            _tempoStreamHandle = BassFx.TempoCreate(handle, BassFlags.SampleOverrideLowestVolume);
            if (_tempoStreamHandle == 0)
            {
                YargLogger.LogFormatError("Failed to create tempo stream: {0}", Bass.LastError);
                return;
            }

            _mixerHandle = handle;
            _shouldNormalize = normalize && Settings.SettingsManager.Settings.EnableNormalization.Value;
            if (_shouldNormalize)
            {
                AddGainDSP();
            }

            _whammySyncTimer = new Timer();
            SetVolume_Internal(volume);
            SetSpeed_Internal(speed, true);
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
                if (!Bass.ChannelPlay(_tempoStreamHandle, _didSetPosition))
                {
                    return (int) Bass.LastError;
                }
                _didSetPosition = false;
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
            Bass.ChannelSlideAttribute(_tempoStreamHandle, ChannelAttribute.Volume, scaled, (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        protected override void FadeOut_Internal(double duration)
        {
            Bass.ChannelSlideAttribute(_tempoStreamHandle, ChannelAttribute.Volume, 0, (int) (duration * SongMetadata.MILLISECOND_FACTOR));
        }

        protected override int Pause_Internal()
        {
            if (!IsPlaying)
            {
                return 0;
            }

            if (!Bass.ChannelPause(_tempoStreamHandle))
            {
                return (int) Bass.LastError;
            }

            return 0;
        }

        protected override double GetPosition_Internal()
        {
            long positionBytes = Bass.ChannelGetPosition(_tempoStreamHandle);
            if (positionBytes < 0)
            {
                YargLogger.LogFormatError("Failed to get byte position: {0}!", Bass.LastError);
                return 0.0f;
            }

            double seconds = Bass.ChannelBytes2Seconds(_tempoStreamHandle, positionBytes);
            if (seconds < 0)
            {
                YargLogger.LogFormatError("Failed to convert bytes to seconds: {0}!", Bass.LastError);
                return 0.0f;
            }

            return seconds + _positionOffset;
        }

        protected override double GetVolume_Internal()
        {
            if (!Bass.ChannelGetAttribute(_tempoStreamHandle, ChannelAttribute.Volume, out float volume))
            {
                YargLogger.LogFormatError("Failed to get volume: {0}", Bass.LastError);
            }
            return BassAudioManager.LogarithmicVolume(volume);
        }

        protected override void SetPosition_Internal(double position)
        {
            var wasPlaying = !IsPlaying;
            Pause_Internal();

            var channels = BassMix.MixerGetChannels(_mixerHandle);
            foreach (var channel in channels)
            {
                if (!BassMix.MixerRemoveChannel(channel))
                {
                    YargLogger.LogDebug("Failed to remove channel from mixer");
                }
            }
            AddChannelsToMixer(_stemDatas);

            foreach (var channel in _channels)
            {
                channel.SetPosition(position);
            }
            _didSetPosition = true;
            _positionOffset = position;

            if (wasPlaying)
            {
                Play_Internal();
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            volume = BassAudioManager.ExponentialVolume(volume);
            if (!Bass.ChannelSetAttribute(_tempoStreamHandle, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set tempo stream volume: {0}", Bass.LastError);
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

            int data = Bass.ChannelGetData(_tempoStreamHandle, buffer, flags);
            if (data < 0)
            {
                return (int) Bass.LastError;
            }
            return data;
        }

        protected override int GetSampleData_Internal(float[] buffer)
        {
            int data = Bass.ChannelGetData(_tempoStreamHandle, buffer, (buffer.Length * 4) | (int) (DataFlags.Float));
            if (data < 0)
            {
                return (int) Bass.LastError;
            }
            return data;
        }

        protected override int GetLevel_Internal(float[] level)
        {
            bool status = Bass.ChannelGetLevel(_tempoStreamHandle, level, 0.2f, LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS);
            if (!status)
            {
                return (int) Bass.LastError;
            }

            return (int) Errors.OK;
        }

        protected override void SetSpeed_Internal(float speed, bool shiftPitch)
        {
            speed = (float) Math.Clamp(speed, 0.05, 50);
            if (_speed == speed)
            {
                return;
            }
            _speed = speed;

            BassAudioManager.SetSpeed(speed, _tempoStreamHandle, shiftPitch);
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
            List<StemData> stemDatas = new();
            foreach (var stemInfo in stemInfos)
            {
                if (!BassAudioManager.CreateSplitStreams(sourceStream, stemInfo.Indices, out var streamHandles,
                    out var reverbHandles))
                {
                    YargLogger.LogFormatError("Failed to load stem {0}: {1}!", stemInfo.Stem, Bass.LastError);
                    continue;
                }
                stemDatas.Add(new StemData(stemInfo.Stem, stemInfo.GetVolumeMatrix(), streamHandles!, reverbHandles!));
            }

            if (!stemDatas.Any())
            {
                YargLogger.LogError("Failed to load any stems!");
                return false;
            }

            if (!AddChannelsToMixer(stemDatas))
            {
                return false;
            }
            _stemDatas.AddRange(stemDatas);

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

        private bool AddChannelsToMixer(IEnumerable<StemData> stemStreamDataList)
        {
            foreach (var stemStreamData in stemStreamDataList)
            {
                var stem = stemStreamData.Stem;
                var streamHandles = stemStreamData.StreamHandles;
                var reverbHandles = stemStreamData.ReverbHandles;
                var volumeMatrix = stemStreamData.VolumeMatrix;

                // Delay any non-pitch bended stem by Whammy FFT size samples to align with pitch bended stems
                long bytes = 0;
                if (GlobalAudioHandler.UseWhammyFx && !AudioHelpers.PitchBendAllowedStems.Contains(stem))
                {
                    Bass.ChannelGetAttribute(streamHandles.Stream, ChannelAttribute.Frequency, out var freq);
                    var seconds = GlobalAudioHandler.WHAMMY_FFT_DEFAULT / freq;
                    bytes = Bass.ChannelSeconds2Bytes(_mixerHandle, seconds);
                }

                var flags = volumeMatrix != null ? BassFlags.MixerChanMatrix : BassFlags.Default;
                if (!BassMix.MixerAddChannel(_mixerHandle, streamHandles.Stream, flags, bytes, 0) ||
                    !BassMix.MixerAddChannel(_mixerHandle, reverbHandles.Stream, flags, bytes, 0))
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
                    YargLogger.LogFormatError("Failed to set {stem} matrices: {0}!", Bass.LastError);
                    return false;
                }
            }
            return true;
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

        protected override void ToggleBuffer_Internal(bool enable)
        {
            _BufferSetter(enable, Bass.PlaybackBufferLength);
        }

        protected override void SetBufferLength_Internal(int length)
        {
            _BufferSetter(SettingsManager.Settings.EnablePlaybackBuffer.Value, length);
        }

        private void _BufferSetter(bool enable, int length)
        {
            if (!enable)
            {
                length = 0;
            }

            if (!Bass.ChannelSetAttribute(_mixerHandle, ChannelAttribute.Buffer, length))
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
                };
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            if (_mixerHandle != 0)
            {
                if (!Bass.StreamFree(_mixerHandle))
                {
                    YargLogger.LogFormatError("Failed to free mixer stream (THIS WILL LEAK MEMORY!): {0}!", Bass.LastError);
                }
            }

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
    }

    #nullable enable
    public static class StreamInfoExtensions
    {
        public static float[,]? GetVolumeMatrix(this StemMixer.StemInfo streamInfo)
        {
            var (_, indices, panning) = streamInfo;

            // Return null if this isn't a multi-channel stream
            if (indices == null || panning == null)
            {
                return null;
            }

            // First array = left pan, second = right pan
            float[,] volumeMatrix = new float[2, indices.Length];

            const int LEFT_PAN = 0;
            const int RIGHT_PAN = 1;

            for (int i = 0; i < indices.Length; ++i)
            {
                volumeMatrix[LEFT_PAN, i] = panning[2 * i];
                volumeMatrix[RIGHT_PAN, i] = panning[2 * i + 1];
            }

            return volumeMatrix;
        }
    }
    #nullable disable
}