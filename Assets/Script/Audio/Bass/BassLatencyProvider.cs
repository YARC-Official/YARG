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
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
                int deviceBufferLength = Math.Max(0, Bass.DeviceBufferLength);
                int devicePeriod = Math.Max(0, Bass.GetConfig(Configuration.DevicePeriod));
                int updatePeriod = Math.Max(0, Bass.UpdatePeriod);
                return (deviceBufferLength + devicePeriod + updatePeriod) / 1000.0;
#elif UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                return DeviceOutputLatency;
#elif UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
                int deviceBufferLength = Math.Max(0, Bass.DeviceBufferLength);
                return deviceBufferLength / 1000.0;
#else
                int deviceBufferLength = Math.Max(0, Bass.DeviceBufferLength);
                return deviceBufferLength / 1000.0;
#endif
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
