using System;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    internal sealed class BassLatencyProvider
    {
        // Additional seek overhead on Windows/Linux to account for WASAPI/ALSA
        // software-side buffering that is not captured by info.Latency or DeviceBufferLength.
        private const double PLATFORM_SEEK_OVERHEAD_SECONDS = 0.015;

        private readonly int _tempoStreamHandle;
        private readonly double _tempoFxLatency;

        private static int PlaybackBufferLength => ClampBufferLength(SettingsManager.Settings?.PlaybackBufferLength.Value ?? 0);

        private static double DeviceOutputLatency     => Math.Max(0, GlobalAudioHandler.PlaybackLatency) / 1000.0;
        private static double ConfiguredOutputLatency => Math.Max(0, PlaybackBufferLength) / 1000.0;
        private static double CommandUpdateLatency    => Math.Max(0, Bass.UpdatePeriod) / 2000.0;
        private static double DeviceBufferLatency     => Math.Max(0, Bass.DeviceBufferLength) / 2000.0;

        public BassLatencyProvider(int tempoStreamHandle)
        {
            _tempoStreamHandle = tempoStreamHandle;

            // Retrieve tempo FX latency from BASS
            if (Bass.ChannelGetAttribute(_tempoStreamHandle, ChannelAttribute.TempoSequenceMilliseconds, out float sequenceMs) &&
                Bass.ChannelGetAttribute(_tempoStreamHandle, ChannelAttribute.TempoSeekWindowMilliseconds, out float seekWindowMs) &&
                Bass.ChannelGetAttribute(_tempoStreamHandle, ChannelAttribute.TempoOverlapMilliseconds, out float overlapMs))
            {
                _tempoFxLatency = (sequenceMs + seekWindowMs + overlapMs) / 1000.0;
            }
            else
            {
                // Default tempo FX latency in seconds, calculated from documented BASS_FX defaults:
                // Sequence (82ms) + Seek Window (14ms) + Overlap (12ms) = 108ms (0.108s)
                _tempoFxLatency = 0.108;
            }
        }

        public double GetPlaybackStreamLatency()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            // CoreAudio is pull-based; info.Latency already encapsulates the full hardware pipeline.
            return DeviceOutputLatency;
#else
            return DeviceOutputLatency + DeviceBufferLatency + PLATFORM_SEEK_OVERHEAD_SECONDS;
#endif
        }

        public double GetTempoStreamLatency()
        {
            return GetOutputBufferLatency() + CommandUpdateLatency + _tempoFxLatency;
        }

        private double GetOutputBufferLatency()
        {
            double maxBufferLatency = ConfiguredOutputLatency;
            if (maxBufferLatency <= 0)
            {
                return 0;
            }

            int availableBytes = Bass.ChannelGetData(_tempoStreamHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (availableBytes < 0)
            {
                return maxBufferLatency;
            }

            double bufferLatency = Bass.ChannelBytes2Seconds(_tempoStreamHandle, availableBytes);
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
