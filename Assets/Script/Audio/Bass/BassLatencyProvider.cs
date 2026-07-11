using System;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Provides estimated BASS tempo stream latency in seconds.
    /// </summary>
    internal static class BassLatencyProvider
    {
        // Estimate command timing at midpoint of BASS's update period.
        private static double CommandLatency => Math.Max(0, Bass.UpdatePeriod) / 2000.0;

        /// <summary>
        /// Gets estimated tempo stream latency, including buffered audio and BASS command latency.
        /// </summary>
        public static double GetTempoStreamLatency(int tempoStreamHandle)
        {
            return GetOutputBufferLatency(tempoStreamHandle) + CommandLatency;
        }

        /// <summary>
        /// Gets time needed for newly started audio to cross BASS and device output buffers.
        /// </summary>
        public static double GetOutputTransitionLatency()
        {
            return Math.Max(0, Bass.Info.Latency + Bass.DeviceBufferLength) / 1000.0;
        }

        private static double GetOutputBufferLatency(int tempoStreamHandle)
        {
            double configuredBufferLatency = BassHelpers.ConfiguredPlaybackBufferLength / 1000.0;
            if (configuredBufferLatency <= 0)
            {
                return 0;
            }

            int availableBytes = Bass.ChannelGetData(tempoStreamHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (availableBytes < 0)
            {
                return configuredBufferLatency;
            }

            double bufferLatency = Bass.ChannelBytes2Seconds(tempoStreamHandle, availableBytes);
            return bufferLatency >= 0 ? bufferLatency : configuredBufferLatency;
        }
    }
}
