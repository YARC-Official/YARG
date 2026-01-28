using System;
using ManagedBass;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;

namespace YARG.Audio.BASS
{
    public sealed class BassSampleChannel : SampleChannel
    {
#nullable enable
        public static BassSampleChannel? Create(SfxSample sample, string path, int playbackCount,
            bool loop = false)
#nullable disable
        {
            BassFlags flags = 0;
            // flags = BassFlags.Decode;

            if (loop)
            {
                flags |= BassFlags.Loop;
            }

            int handle = Bass.SampleLoad(path, 0, 0, playbackCount, flags);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} {1}: {2}!", sample, path, Bass.LastError);
                return null;
            }

            int channel = Bass.SampleGetChannel(handle);
            if (channel == 0)
            {
                Bass.SampleFree(handle);
                YargLogger.LogFormatError("Failed to create {0} channel: {1}!", sample, Bass.LastError);
                return null;
            }

            var volume = AudioHelpers.SfxSamples[(int) sample].Volume;

            if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", sample, Bass.LastError);
            }

            return new BassSampleChannel(handle, channel, sample, path, playbackCount);
        }

        private readonly int _sfxHandle;
        private readonly int _channel;
        private double _lastPlaybackTime;

        private double _volumeSetting = 1;

        private int _syncHandle;

        private BassSampleChannel(int handle, int channel, SfxSample sample, string path, int playbackCount)
            : base(sample, path, playbackCount)
        {
            _sfxHandle = handle;
            _channel = channel;
            _lastPlaybackTime = -1;
        }

        protected override void Play_Internal(double duration)
        {
            // Suppress playback if the last instance of this sample was too recent
            if (InputManager.CurrentInputTime - _lastPlaybackTime < PLAYBACK_SUPPRESS_THRESHOLD)
            {
                return;
            }

            // In case we previously disabled looping
            if (AudioHelpers.SfxSamples[(int) Sample].CanLoop)
            {
                Bass.ChannelAddFlag(_channel, BassFlags.Loop);
            }

            if (duration > 0)
            {
                var time = (int) Math.Round(duration * 1000);
                Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, 0);
                var sfxVolume = AudioHelpers.SfxSamples[(int) Sample].Volume * (float) _volumeSetting;
                if (!Bass.ChannelSlideAttribute(_channel, ChannelAttribute.Volume, sfxVolume, time))
                {
                    YargLogger.LogFormatError("Failed to set volume slide for {0}: {1}!", Sample, Bass.LastError);
                }
            }
            else
            {
                // Since we might have reset the volume at some point, we need to put it back to default
                SetVolume_Internal(_volumeSetting);
            }

            if (!Bass.ChannelPlay(_channel, true))
            {
                YargLogger.LogFormatError("Failed to play {0} channel: {1}!", Sample, Bass.LastError);
            }
            else
            {
                var sfxSample = AudioHelpers.SfxSamples[(int) Sample];
                sfxSample.IsPlaying = true;
            }

            if (duration > 0 && !Bass.ChannelIsSliding(_channel, ChannelAttribute.Volume))
            {
                YargLogger.LogFormatError("Failed to set volume slide for {0} even though duration is set!", Sample);
            }

            _lastPlaybackTime = InputManager.CurrentInputTime;
        }

        protected override void Stop_Internal(double duration)
        {
            // Check if the channel is playing
            if (Bass.ChannelIsActive(_channel) is not (PlaybackState.Playing or PlaybackState.Stalled))
            {
                return;
            }

            if (duration > 0)
            {
                // Disable looping for this channel so it will eventually stop wasting resources
                Bass.ChannelRemoveFlag(_channel, BassFlags.Loop);

                var time = (int) Math.Round(duration * 1000);

                if (!Bass.ChannelSlideAttribute(_channel, ChannelAttribute.Volume, 0, time))
                {
                    YargLogger.LogFormatError("Failed to set volume slide for {0}: {1}!", Sample, Bass.LastError);
                }

                var sfxSample = AudioHelpers.SfxSamples[(int) Sample];
                sfxSample.IsPlaying = false;

                // Let it stop when it comes to the end since it will be silent anyway
                return;
            }

            // We pause rather than stop so it can be restarted later
            if (!Bass.ChannelPause(_channel))
            {
                YargLogger.LogFormatError("Failed to stop {0} channel: {1}!", Sample, Bass.LastError);
            }
            else
            {
                var sfxSample = AudioHelpers.SfxSamples[(int) Sample];
                sfxSample.IsPlaying = false;
            }
        }

        protected override void Pause_Internal()
        {
            if (Bass.ChannelIsActive(_channel) is not PlaybackState.Playing)
            {
                return;
            }

            if (!Bass.ChannelPause(_channel))
            {
                YargLogger.LogFormatError("Failed to pause {0} channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void Resume_Internal()
        {
            if (!AudioHelpers.SfxSamples[(int) Sample].IsPlaying)
            {
                return;
            }

            if (Bass.ChannelIsActive(_channel) is not PlaybackState.Paused)
            {
                return;
            }

            if (!Bass.ChannelPlay(_channel, false))
            {
                YargLogger.LogFormatError("Failed to resume {0} channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            _volumeSetting = volume;
            volume *= AudioHelpers.SfxSamples[(int) Sample].Volume;
            if (!Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void SetEndCallback_Internal()
        {
            if (_syncHandle != 0)
            {
                YargLogger.LogWarning("Attempted to set end callback for {0}, but it was already set!");
                return;
            }

            _syncHandle = Bass.ChannelSetSync(_channel, SyncFlags.End, 0, EndCallback);
            if (_syncHandle == 0)
            {
                YargLogger.LogFormatError("Failed to set {0} end callback: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void EndCallback_Internal(int _, int __, int ___, IntPtr ____)
        {
            var sfxSample = AudioHelpers.SfxSamples[(int) Sample];
            sfxSample.IsPlaying = false;
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_sfxHandle);
        }
    }
}