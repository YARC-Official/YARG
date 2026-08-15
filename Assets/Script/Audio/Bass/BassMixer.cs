#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Wraps a BASS mixer stream to combine multiple audio sources (stems, sound effects, or monitors)
    ///     into a single decoded or playback stream with multi-threaded processing support.
    /// </summary>
    internal sealed class BassMixer : IDisposable
    {
        private readonly List<BassMixerSource> _sources = new();
        private readonly float[]               _level   = new float[1];

        private bool _disposed;

        private BassMixer(int handle, int sampleRate)
        {
            Handle = handle;
            SampleRate = sampleRate;
        }

        public int Handle     { get; }
        public int SampleRate { get; }

        public static BassMixer? Create(int sampleRate, int channelCount, BassFlags flags,
            int processingThreads = 0)
        {
            int handle = BassMix.CreateMixerStream(sampleRate, channelCount, flags);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to create mixer: {0}", Bass.LastError);
                return null;
            }

            var mixer = new BassMixer(handle, sampleRate);
            if (processingThreads > 0 && !mixer.SetProcessingThreads(processingThreads))
            {
                mixer.Dispose();
                return null;
            }

            return mixer;
        }

        public BassMixerSource? CreateSource(Stream stream)
        {
            if (_disposed)
            {
                return null;
            }

            int handle = BassX.CreateSourceUnchecked(stream);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to create source stream: {0}", Bass.LastError);
                return null;
            }

            var source = new BassMixerSource(this, handle);
            _sources.Add(source);
            return source;
        }

        public bool AddStream(Stream stream, params StemMixer.StemInfo[] stemInfos)
        {
            if (_disposed || stemInfos.Length == 0)
            {
                return false;
            }

            var source = CreateSource(stream);
            if (source == null)
            {
                return false;
            }

            var channels = new List<BassMixerChannel>();
            foreach (var stemInfo in stemInfos)
            {
                var volumeMatrix = BassHelpers.BuildVolumeMatrix(stemInfo);
                if (volumeMatrix == null)
                {
                    channels.Add(new BassMixerChannel(source.Handle));
                    continue;
                }

                var split = source.CreateSplit(stemInfo.Indices!);
                if (split == null)
                {
                    source.Dispose();
                    return false;
                }

                channels.Add(new BassMixerChannel(split.Stream, volumeMatrix));
            }

            if (AddChannels(channels))
            {
                return true;
            }

            source.Dispose();
            return false;
        }

        public bool AddChannel(int channelHandle, BassFlags flags = BassFlags.Default, long delayBytes = 0)
        {
            if (_disposed)
            {
                return false;
            }

            if (!BassMix.MixerAddChannel(Handle, channelHandle, flags, delayBytes, 0))
            {
                YargLogger.LogFormatError("Failed to add channel {0} to mixer: {1}", channelHandle, Bass.LastError);
                return false;
            }

            return true;
        }

        public bool AddChannels(IEnumerable<BassMixerChannel> channels)
        {
            if (_disposed)
            {
                return false;
            }

            var addedHandles = new List<int>();
            try
            {
                foreach (var channel in channels)
                {
                    long delayBytes = Bass.ChannelSeconds2Bytes(Handle, channel.DelaySeconds);
                    var flags = channel.VolumeMatrix == null ? BassFlags.Default : BassFlags.MixerChanMatrix;

                    BassX.Require(BassMix.MixerAddChannel(Handle, channel.Handle, flags, delayBytes, 0),
                        $"add channel {channel.Handle} to mixer {Handle}");
                    addedHandles.Add(channel.Handle);

                    if (channel.VolumeMatrix != null)
                    {
                        BassX.Require(BassMix.ChannelSetMatrix(channel.Handle, channel.VolumeMatrix),
                            $"set volume matrix for channel {channel.Handle}");
                    }
                }

                return true;
            }
            catch (BassX.BassOperationException exception)
            {
                YargLogger.LogError(exception.Message);
                RemoveChannels(addedHandles);
                return false;
            }
        }

        public void RemoveAllChannels()
        {
            if (!_disposed)
            {
                RemoveChannels(BassMix.MixerGetChannels(Handle));
            }
        }

        public bool RemoveChannel(int handle)
        {
            if (_disposed)
            {
                return false;
            }

            if (!BassMix.MixerRemoveChannel(handle) && Bass.LastError != Errors.Handle)
            {
                YargLogger.LogFormatError("Failed to remove channel {0} from mixer: {1}", handle, Bass.LastError);
                return false;
            }

            return true;
        }

        public void RemoveChannels(IEnumerable<int> handles)
        {
            foreach (int handle in handles)
            {
                RemoveChannel(handle);
            }
        }

        public bool SetPositionBytes(long position) => !_disposed && Bass.ChannelSetPosition(Handle, position);

        public bool SetAttribute(ChannelAttribute attribute, float value) =>
            !_disposed && Bass.ChannelSetAttribute(Handle, attribute, value);

        public bool GetAttribute(ChannelAttribute attribute, out float value)
        {
            value = 0;
            return !_disposed && Bass.ChannelGetAttribute(Handle, attribute, out value);
        }

        public bool SlideAttribute(ChannelAttribute attribute, float value, int durationMilliseconds) =>
            !_disposed && Bass.ChannelSlideAttribute(Handle, attribute, value, durationMilliseconds);

        public bool SetFlags(BassFlags flags, BassFlags mask) =>
            !_disposed && BassMix.ChannelFlags(Handle, flags, mask) >= 0;

        public bool TryGetRms(float windowSeconds, out float rms)
        {
            rms = 0;
            if (_disposed || !Bass.ChannelGetLevel(Handle, _level, windowSeconds,
                    LevelRetrievalFlags.Mono | LevelRetrievalFlags.RMS))
            {
                return false;
            }

            rms = _level[0];
            return true;
        }

        public bool SetProcessingThreads(int count) => !_disposed && BassX.SetProcessingThreads(Handle, count);

        internal void SetDevice(int deviceId)
        {
            if (_disposed)
            {
                return;
            }

            foreach (var source in _sources)
            {
                source.SetDevice(deviceId);
            }

            BassX.Check(Bass.ChannelSetDevice(Handle, deviceId),
                $"move mixer {Handle} to device {deviceId}");
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            while (_sources.Count > 0)
            {
                _sources[^1].Dispose();
            }

            BassX.Check(Bass.StreamFree(Handle), $"free mixer {Handle}");
        }

        internal void RemoveSource(BassMixerSource source) => _sources.Remove(source);
    }
}
