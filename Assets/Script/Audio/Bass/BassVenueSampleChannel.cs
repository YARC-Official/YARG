using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    public sealed class BassVenueSampleChannel : VenueSampleChannel
    {
        private readonly int _sampleHandle;
        private readonly int _channel;

        private bool _isPlaying;

        public static BassVenueSampleChannel? Create(string name, byte[] sampleData, OutputChannel? outputChannel)
        {
            int handle = Bass.SampleLoad(sampleData, 0, sampleData.Length, 2, BassFlags.Default);

            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load venue sample {0}: {1}", name, Bass.LastError);
                return null;
            }

            int channel = Bass.SampleGetChannel(handle);
            if (channel == 0)
            {
                Bass.SampleFree(handle);
                YargLogger.LogFormatError("Failed to create {0} channel: {1}!", name, Bass.LastError);
                return null;
            }

            var volume = GlobalAudioHandler.GetTrueVolume(SongStem.VenueSample);
            if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", name, Bass.LastError);
            }

            return new BassVenueSampleChannel(handle, channel, name, sampleData, outputChannel);
        }

        private int _syncHandle;

        public BassVenueSampleChannel(int handle, int channel, string name, byte[] sampleData, OutputChannel? outputChannel)
            : base(name, sampleData)
        {
            _sampleHandle = handle;
            _channel = channel;
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.VenueSample));
        }

        protected override void Play_Internal()
        {
            if (!Bass.ChannelPlay(_channel, true))
            {
                YargLogger.LogFormatError("Failed to play {0} channel: {1}!", SampleName, Bass.LastError);
                return;
            }

            _isPlaying = true;
        }

        protected override void Pause_Internal()
        {
            if (!_isPlaying || !IsPlaying_Internal())
            {
                return;
            }

            if (!Bass.ChannelPause(_channel))
            {
                YargLogger.LogFormatError("Failed to pause {0} channel: {1}!", SampleName, Bass.LastError);
            }
        }

        protected override void Resume_Internal()
        {
            if (!_isPlaying || !IsPaused_Internal())
            {
                return;
            }

            if (!Bass.ChannelPlay(_channel, false))
            {
                YargLogger.LogFormatError("Failed to resume {0} channel: {1}!", SampleName, Bass.LastError);
            }
        }

        protected override void Stop_Internal()
        {
            if (!_isPlaying || (!IsPlaying_Internal() && !IsPaused_Internal()))
            {
                return;
            }

            if (!Bass.ChannelStop(_channel))
            {
                YargLogger.LogFormatError("Failed to stop {0} channel: {1}!", SampleName, Bass.LastError);
            }

            _isPlaying = false;
        }

        protected override void SetVolume_Internal(double volume)
        {
            if (!Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, (float)volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", SampleName, Bass.LastError);
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
                YargLogger.LogFormatError("Failed to set {0} end callback: {1}!", SampleName, Bass.LastError);
            }
        }

        protected override void SetOutputChannel_Internal(OutputChannel channel)
        {
            BassHelpers.UpdateOutputChannels(_channel, channel);
        }

        protected override void EndCallback_Internal(int _, int __, int ___, IntPtr ____)
        {
            _isPlaying = false;
        }

        protected override bool IsPlaying_Internal()
        {
            return Bass.ChannelIsActive(_channel) is PlaybackState.Playing or PlaybackState.Stalled;
        }

        protected override bool IsPaused_Internal()
        {
            return Bass.ChannelIsActive(_channel) is PlaybackState.Paused;
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_sampleHandle);
        }
    }
}