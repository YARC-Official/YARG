using System;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Provides estimated BASS tempo stream latency in seconds.
    /// </summary>
    internal static class BassLatencyProvider
    {
        // Default BASS_FX tempo latency: sequence (82 ms) + seek window (14 ms) + overlap (12 ms).
        private const double TEMPO_FX_LATENCY_SECONDS = 0.108;

        // Half the update period is the best estimate for latency whose exact position in the period is unknown.
        private static double CommandLatency => Math.Max(0, Bass.UpdatePeriod) / 2000.0;

        /// <summary>
        /// Gets estimated tempo stream latency, including buffered audio, BASS command latency,
        /// and BASS_FX tempo processing latency.
        /// </summary>
        public static double GetTempoStreamLatency(int tempoStreamHandle)
        {
            return GetOutputBufferLatency(tempoStreamHandle) + CommandLatency + TEMPO_FX_LATENCY_SECONDS;
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
