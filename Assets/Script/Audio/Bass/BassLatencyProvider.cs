using System;
using ManagedBass;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Provides estimated BASS playback and tempo stream latencies in seconds.
    /// </summary>
    internal static class BassLatencyProvider
    {
        private static double DeviceOutputLatency => Math.Max(0, Bass.Info.Latency) / 1000.0;

        // Estimate command timing at midpoint of BASS's update period.
        private static double CommandLatency => Math.Max(0, Bass.UpdatePeriod) / 2000.0;

        /// <summary>
        /// Gets estimated playback stream output latency.
        /// </summary>
        public static double GetPlaybackStreamLatency()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // CoreAudio is pull-based; info.Latency already encapsulates the full hardware pipeline.
            return DeviceOutputLatency;
#else
            double deviceBufferLatency = Math.Max(0, Bass.DeviceBufferLength) / 1000.0;
            return DeviceOutputLatency + deviceBufferLatency;
#endif
        }

        /// <summary>
        /// Gets estimated tempo stream latency, including buffered audio and BASS command latency.
        /// </summary>
        public static double GetTempoStreamLatency(int tempoStreamHandle)
        {
            return GetOutputBufferLatency(tempoStreamHandle) + CommandLatency;
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
