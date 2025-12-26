using ManagedBass;
using UnityEngine;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Input;

namespace YARG.Audio.BASS
{
    public sealed class BassMetronomeSampleChannel : MetronomeSampleChannel
    {
        public static BassMetronomeSampleChannel? Create(MetronomeSample sample, string path)
        {
            int handle = Bass.SampleLoad(path, 0, 0, 1, BassFlags.Decode);
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

            return new BassMetronomeSampleChannel(handle, channel, sample, path);
        }

        private readonly int _handle;
        private readonly int _channel;

        private BassMetronomeSampleChannel(int handle, int channel, MetronomeSample sample, string path)
            : base(sample, path)
        {
            _handle = handle;
            _channel = channel;
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
            if (!Bass.ChannelSetAttribute(_channel, ChannelAttribute.Volume, volume))
            {
                YargLogger.LogFormatError("Failed to set {0} volume: {1}!", Sample, Bass.LastError);
            }
        }

        protected override void DisposeUnmanagedResources()
        {
            Bass.SampleFree(_handle);
        }
    }
}