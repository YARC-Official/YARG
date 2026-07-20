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
        /// Gets delay before a newly played stream's compensated position begins advancing.
        /// </summary>
        public static double StartupLatency
        {
            get
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                // BASS_CONFIG_DEV_BUFFER cannot be queried on macOS. BassAudioManager configures
                // it to twice the device period during initialization.
                int deviceBufferLength = 2 * Bass.GetConfig(Configuration.DevicePeriod);
#else
                int deviceBufferLength = Bass.DeviceBufferLength;
#endif
                return Math.Max(0, deviceBufferLength) / 1000.0;
            }
        }

        /// <summary>
        /// Gets estimated playback stream output latency.
        /// </summary>
        public static double GetPlaybackStreamLatency()
        {
            return DeviceOutputLatency;
        }

        /// <summary>
        /// Gets estimated buffered playback latency, including BASS command latency.
        /// Method name is retained for the existing StemMixer API.
        /// </summary>
        public static double GetTempoStreamLatency(int playbackStreamHandle)
        {
            return GetOutputBufferLatency(playbackStreamHandle) + CommandLatency;
        }

        private static double GetOutputBufferLatency(int playbackStreamHandle)
        {
            double configuredBufferLatency = BassHelpers.ConfiguredPlaybackBufferLength / 1000.0;
            if (configuredBufferLatency <= 0)
            {
                return 0;
            }

            int availableBytes = Bass.ChannelGetData(playbackStreamHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (availableBytes < 0)
            {
                return configuredBufferLatency;
            }

            double bufferLatency = Bass.ChannelBytes2Seconds(playbackStreamHandle, availableBytes);
            return bufferLatency >= 0 ? bufferLatency : configuredBufferLatency;
        }
    }
}
