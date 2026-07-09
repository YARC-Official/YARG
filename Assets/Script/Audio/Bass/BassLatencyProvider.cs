using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    /// <summary>
    /// Provides estimated BASS playback and tempo stream latencies in seconds.
    /// </summary>
    internal static class BassLatencyProvider
    {
        // Additional seek overhead on Windows/Linux to account for WASAPI/ALSA
        // buffering that is not captured by info.Latency or DeviceBufferLength.  Determined by
        // measuring the actual latency of a seek call
        private const double EXTRA_LATENCY_SECONDS = 0.015;

        // Default BASS_FX tempo latency in seconds, calculated from documented defaults:
        // Sequence (82ms) + Seek Window (14ms) + Overlap (12ms) = 108ms.
        private const double TEMPO_FX_LATENCY_SECONDS = 0.108;

        // User-configured playback buffer length in ms, clamped to BASS minimum buffer requirements.
        private static int PlaybackBufferLengthMs => ClampBufferLength(SettingsManager.Settings?.PlaybackBufferLength.Value ?? 0);

        // Device-reported output latency in seconds.
        private static double DeviceOutputLatency     => Math.Max(0, Bass.Info.Latency) / 1000.0;

        // Configured playback buffer length in seconds.
        private static double ConfiguredOutputLatency => Math.Max(0, PlaybackBufferLengthMs) / 1000.0;

        // BASS update period latency in seconds; / 2000 gives statistically best guess at uncertain latency.
        private static double CommandLatency    => Math.Max(0, Bass.UpdatePeriod) / 2000.0;

        // BASS device buffer latency in seconds; / 2000 gives statistically best guess at uncertain latency.
        private static double DeviceBufferLatency     => Math.Max(0, Bass.DeviceBufferLength) / 2000.0;

        /// <summary>
        /// Gets the estimated playback stream output latency in seconds.
        /// </summary>
        public static double GetPlaybackStreamLatency()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // CoreAudio is pull-based; info.Latency already encapsulates the full hardware pipeline.
            double latency = DeviceOutputLatency;
#else
            double latency = DeviceOutputLatency + DeviceBufferLatency + EXTRA_LATENCY_SECONDS;
#endif
            return latency;
        }

        /// <summary>
        /// Gets the estimated tempo stream latency in seconds, including buffered audio, BASS command latency,
        /// and BASS_FX tempo processing latency.
        /// </summary>
        /// <param name="tempoStreamHandle">The BASS tempo stream handle.</param>
        public static double GetTempoStreamLatency(int tempoStreamHandle)
        {
            return GetOutputBufferLatency(tempoStreamHandle) + CommandLatency + TEMPO_FX_LATENCY_SECONDS;
        }

        private static double GetOutputBufferLatency(int tempoStreamHandle)
        {
            double maxBufferLatency = ConfiguredOutputLatency;
            if (maxBufferLatency <= 0)
            {
                return 0;
            }

            int availableBytes = Bass.ChannelGetData(tempoStreamHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (availableBytes < 0)
            {
                return maxBufferLatency;
            }

            double bufferLatency = Bass.ChannelBytes2Seconds(tempoStreamHandle, availableBytes);
            if (bufferLatency < 0)
            {
                return maxBufferLatency;
            }

            return bufferLatency;
        }


        private static int ClampBufferLength(int length)
        {
            int minimumLength = GlobalAudioHandler.MinimumBufferLength;
            if (length > 0 && minimumLength > 0 && length < minimumLength)
            {
                return minimumLength;
            }

            return length;
        }
    }
}
