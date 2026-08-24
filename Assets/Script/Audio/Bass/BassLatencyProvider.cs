using System;
using ManagedBass;

namespace YARG.Audio.BASS
{
    internal static class BassLatencyProvider
    {
        private static double DeviceOutputLatency => Math.Max(0, Bass.Info.Latency) / 1000.0;

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
#else
                int deviceBufferLength = Math.Max(0, Bass.DeviceBufferLength);
                return deviceBufferLength / 1000.0;
#endif
            }
        }
    }
}
