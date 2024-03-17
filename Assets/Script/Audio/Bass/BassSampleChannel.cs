using System;
using Cysharp.Threading.Tasks;
using ManagedBass;
using UnityEngine;
using UnityEngine.Rendering;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;

namespace YARG.Audio.BASS
{
    public sealed class BassSampleChannel : SampleChannel
    {
        public static BassSampleChannel? Create(BassAudioManager manager, SfxSample sample, string path, int playbackCount)
        {
            int handle = Bass.SampleLoad(path, 0, 0, playbackCount, BassFlags.Decode);
            if (handle == 0)
            {
                YargLogger.LogFormatError("Failed to load {sample} {path}: {0}!", Bass.LastError);
                return null;
            }

            int channel = Bass.SampleGetChannel(handle);
            if (channel == 0)
            {
                Bass.SampleFree(handle);
                YargLogger.LogFormatError("Failed to create {sample} channel: {0}!", Bass.LastError);
                return null;
            }

            if (!Bass.ChannelSetAttribute(channel, ChannelAttribute.Volume, AudioHelpers.SfxVolume[(int) sample]))
            {
                YargLogger.LogFormatError("Failed to set {sample} volume: {0}!", Bass.LastError);
            }

            return new BassSampleChannel(manager, handle, channel, sample, path, playbackCount);
        }

        private readonly BassAudioManager _manager;
        private readonly int _sfxHandle;
        private readonly int _channel;
        private double _lastPlaybackTime;

        private BassSampleChannel(BassAudioManager manager, int handle, int channel, SfxSample sample, string path, int playbackCount)
            : base(sample, path, playbackCount)
        {
            _manager = manager;
            _sfxHandle = handle;
            _channel = channel;
            _lastPlaybackTime = -1;
        }

        protected override void Play_Internal()
        {
            // Suppress playback if the last instance of this sample was too recent
            if (InputManager.CurrentInputTime - _lastPlaybackTime < PLAYBACK_SUPPRESS_THRESHOLD)
            {
                return;
            }

            if (!Bass.ChannelPlay(_channel, true))
            {
                YargLogger.LogFormatError("Failed to play {Sample} channel: {0}!", Bass.LastError);
            }

            _lastPlaybackTime = InputManager.CurrentInputTime;
        }

        protected override void SetVolume_Internal(double volume)
        {
            volume *= AudioHelpers.SfxVolume[(int) Sample];
            if (!Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {Sample} volume: {0}!", Bass.LastError);
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_sfxHandle);
        }
    }
}