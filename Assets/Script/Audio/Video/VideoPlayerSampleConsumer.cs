#nullable enable
using System;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.Experimental.Audio;
using UnityEngine.Experimental.Video;
using UnityEngine.Video;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio
{
    /// <summary>
    ///     Consumes samples from a <see cref="VideoPlayer" /> (in
    ///     <see cref="VideoAudioOutputMode.APIOnly" /> mode), placing them into a BASS push stream,
    ///     so the video's audio is mixed through YARG's main output instead of being dropped.
    ///
    ///     Owner must create the bridge in the player's <c>prepareCompleted</c> callback
    ///     (the track's sample rate/channels are only known once the video is prepared)
    ///     and dispose it before the player is stopped or destroyed.
    /// </summary>
    public sealed class VideoPlayerSampleConsumer : IDisposable
    {
        private readonly VideoPlayer      _player;
        private readonly SongStem         _stem;
        private readonly ushort           _trackIndex;
        private          NativeArray<float> _buffer;
        private          AudioSampleProvider? _provider;
        private          PushStream?      _pushStream;
        private          bool             _initialized;
        private          bool             _disposed;

        public VideoPlayerSampleConsumer(VideoPlayer player, ushort trackIndex = 0, SongStem stem = SongStem.Sfx)
        {
            _player = player;
            _trackIndex = trackIndex;
            _stem = stem;
        }

        public uint SampleRate { get; private set; }

        public int ChannelCount { get; private set; }

        /// <summary>
        ///  Creates the push stream and subscribes to the provider's sample events. Must be called after
        ///  the video is prepared. Reports no-op when the video has no audio track or the audio system is
        ///  not available, so callers can always fall back to silent video.
        /// </summary>
        public bool Initialize()
        {
            if (_disposed || _initialized)
            {
                return _initialized;
            }

            var provider = _player.GetAudioSampleProvider(_trackIndex);
            if (provider == null || !provider.valid || provider.sampleRate == 0)
            {
                YargLogger.LogInfo("Video has no usable audio track; playing silently");
                return false;
            }

            PushStream? stream;
            try
            {
                stream = GlobalAudioHandler.CreatePushStream((int) provider.sampleRate, provider.channelCount);
            }
            catch (Exception e)
            {
                YargLogger.LogFormatError("Failed to create push stream for video audio: {0}", e.Message);
                return false;
            }

            _provider = provider;
            _pushStream = stream;
            SampleRate = provider.sampleRate;
            ChannelCount = provider.channelCount;
            _buffer = new NativeArray<float>((int) provider.maxSampleFrameCount * ChannelCount, Allocator.Persistent);

            provider.enableSampleFramesAvailableEvents = true;
            provider.sampleFramesAvailable += OnSampleFramesAvailable;
            provider.sampleFramesOverflow += OnSampleFramesOverflow;

            stream.SetVolume(GlobalAudioHandler.GetTrueVolume(_stem));
            SettingsManager.Settings.SfxVolume.OnChange += OnVolumeChanged;

            _initialized = true;
            return true;
        }

        /// <summary>
        /// Discards unplayed audio so a seek does not leave stale samples in the output.
        /// </summary>
        public void Flush()
        {
            _pushStream?.Clear();
        }

        private unsafe void OnSampleFramesAvailable(AudioSampleProvider provider, uint sampleFrameCount)
        {
            if (_disposed || _pushStream == null || !_buffer.IsCreated || provider.availableSampleFrameCount == 0)
            {
                return;
            }

            uint consumed = provider.ConsumeSampleFrames(_buffer);
            if (consumed == 0)
            {
                return;
            }

            _pushStream.Push((IntPtr) _buffer.GetUnsafeReadOnlyPtr(),
                             (int) (consumed * (uint) ChannelCount * sizeof(float)));
        }

        private void OnSampleFramesOverflow(AudioSampleProvider provider, uint sampleFrameCount)
        {
            YargLogger.LogFormatWarning("Video audio overflowed: dropped {0} frames", sampleFrameCount);
        }

        private void OnVolumeChanged(float _)
        {
            _pushStream?.SetVolume(GlobalAudioHandler.GetTrueVolume(_stem));
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _initialized = false;

            if (_provider != null)
            {
                _provider.sampleFramesAvailable -= OnSampleFramesAvailable;
                _provider.sampleFramesOverflow -= OnSampleFramesOverflow;
                _provider = null;
            }

            SettingsManager.Settings.SfxVolume.OnChange -= OnVolumeChanged;

            _buffer.Dispose();
            _pushStream?.Dispose();
            _pushStream = null;
        }
    }
}
