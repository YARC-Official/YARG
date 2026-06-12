using System;
using ManagedBass;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public sealed class BassDrumSampleChannel : DrumSampleChannel
    {
#nullable enable
        public static BassDrumSampleChannel? Create(DrumSfxSample sample, string path, int playbackCount, OutputChannel? outputChannel)
#nullable disable
        {
            int handle = Bass.SampleLoad(path, 0, 0, playbackCount, BassFlags.Decode);
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

            float normMultiplier = BassHelpers.GetNormMultiplier(path);

            return new BassDrumSampleChannel(handle, channel, sample, path, playbackCount, outputChannel, normMultiplier);
        }

        private readonly int _sfxHandle;
        private readonly int _channel;
        private readonly float _normalizationMultiplier;

#nullable enable
        private BassDrumSampleChannel(int handle, int channel, DrumSfxSample sample, string path, int playbackCount, OutputChannel? outputChannel, float normalizationMultiplier)
            : base(sample, path, playbackCount)
#nullable disable
        {
            _sfxHandle = handle;
            _channel = channel;
            _normalizationMultiplier = normalizationMultiplier;
            SetOutputChannel_Internal(outputChannel);
        }

        protected override void Play_Internal()
        {
            if (!Bass.ChannelPlay(_channel, true))
            {
                YargLogger.LogFormatError("Failed to play {0} channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            volume *= _normalizationMultiplier;
            if (!Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", Sample, Bass.LastError);
            }
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            BassHelpers.UpdateOutputChannels(_channel, channel);
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_sfxHandle);
        }
    }
}