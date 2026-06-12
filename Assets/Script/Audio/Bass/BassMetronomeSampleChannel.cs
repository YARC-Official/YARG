using System;
using ManagedBass;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    public sealed class BassMetronomeSampleChannel : MetronomeSampleChannel
    {
#nullable enable
        public static BassMetronomeSampleChannel? Create(MetronomeSample sample, string hiPath, string loPath,
             OutputChannel? outputChannel)
#nullable disable
        {
            int hiHandle = Bass.SampleLoad(hiPath, 0, 0, 1, BassFlags.Decode);
            if (hiHandle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} hi {1}: {2}!", sample, hiPath, Bass.LastError);
                return null;
            }

            int hiChannel = Bass.SampleGetChannel(hiHandle);
            if (hiChannel == 0)
            {
                Bass.SampleFree(hiHandle);
                YargLogger.LogFormatError("Failed to create {0} hi channel: {1}!", sample, Bass.LastError);
                return null;
            }

            int loHandle = Bass.SampleLoad(loPath, 0, 0, 1, BassFlags.Decode);
            if (loHandle == 0)
            {
                YargLogger.LogFormatError("Failed to load {0} lo {1}: {2}!", sample, loPath, Bass.LastError);
                return null;
            }

            int loChannel = Bass.SampleGetChannel(loHandle);
            if (loChannel == 0)
            {
                Bass.SampleFree(loHandle);
                YargLogger.LogFormatError("Failed to create {0} lo channel: {1}!", sample, Bass.LastError);
                return null;
            }

            float hiNormMultiplier = BassHelpers.GetNormMultiplier(hiPath);
            float loNormMultiplier = BassHelpers.GetNormMultiplier(loPath);

            return new BassMetronomeSampleChannel(sample, hiHandle, hiChannel, hiPath, loHandle, loChannel, loPath, outputChannel, hiNormMultiplier, loNormMultiplier);
        }

        private readonly int _hiHandle;
        private readonly int _hiChannel;
        private readonly int _loHandle;
        private readonly int _loChannel;
        private double _volumeSetting = 1;
        private readonly float _hiNormalizationMultiplier;
        private readonly float _loNormalizationMultiplier;

#nullable enable
        private BassMetronomeSampleChannel(MetronomeSample sample, int hiHandle, int hiChannel, string hiPath, int loHandle, int loChannel, string loPath,
            OutputChannel? outputChannel, float hiNormalizationMultiplier, float loNormalizationMultiplier)
            : base(sample, hiPath, loPath)
#nullable disable
        {
            _hiHandle = hiHandle;
            _hiChannel = hiChannel;
            _loHandle = loHandle;
            _loChannel = loChannel;
            _hiNormalizationMultiplier = hiNormalizationMultiplier;
            _loNormalizationMultiplier = loNormalizationMultiplier;
            SetOutputChannel_Internal(outputChannel);
            SetVolume_Internal(GlobalAudioHandler.GetTrueVolume(SongStem.Metronome));
        }

        protected override void PlayHi_Internal()
        {
            SetVolume_Internal(_volumeSetting);
            if (!Bass.ChannelPlay(_hiChannel, true))
            {
                YargLogger.LogFormatError("Failed to play {0} hi channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void PlayLo_Internal()
        {
            SetVolume_Internal(_volumeSetting);
            if (!Bass.ChannelPlay(_loChannel, true))
            {
                YargLogger.LogFormatError("Failed to play {0} lo channel: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void SetVolume_Internal(double volume)
        {
            _volumeSetting = volume;
            volume *= AudioHelpers.MetronomeSamples[(int) Sample].Volume;

            if (!Bass.ChannelSetAttribute(_hiChannel, ChannelAttribute.Volume, volume * _hiNormalizationMultiplier))
            {
                YargLogger.LogFormatError("Failed to set {0} hi volume: {1}!", Sample, Bass.LastError);
            }

            if (!Bass.ChannelSetAttribute(_loChannel, ChannelAttribute.Volume, volume * _loNormalizationMultiplier))
            {
                YargLogger.LogFormatError("Failed to set {0} lo volume: {1}!", Sample, Bass.LastError);
            }
        }

#nullable enable
        protected override void SetOutputChannel_Internal(OutputChannel? channel)
#nullable disable
        {
            BassHelpers.UpdateOutputChannels(_hiChannel, channel);
            BassHelpers.UpdateOutputChannels(_loChannel, channel);
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_hiHandle);
            Bass.SampleFree(_loHandle);
        }
    }
}