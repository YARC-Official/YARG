#nullable enable
using System;
using System.Threading;
using ManagedBass;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Probes a BASS recording device across different sample rates and channel counts to determine the
    ///     maximum number of distinct physical input channels available.
    /// </summary>
    internal sealed class BassMicChannelProbe : IDisposable
    {
        private const int TIMEOUT_MS = 400;

        private static readonly (int Channels, int Rate)[] PROBE_CONFIGS =
        {
            (8, 48000),
            (8, 44100),
            (2, 48000),
            (2, 44100),
            (1, 48000),
            (1, 44100),
        };

        private readonly ManualResetEventSlim _frameReceived = new(false);
        private readonly int                  _reportedChannelCount;
        private          short[]              _latestFrame = Array.Empty<short>();

        private BassMicChannelProbe(int reportedChannelCount)
        {
            _reportedChannelCount = reportedChannelCount;
        }

        public void Dispose() => _frameReceived.Dispose();

        public static int? DetectChannelCount(int deviceId, string name)
        {
            bool initialized = Bass.RecordInit(deviceId);
            if (!initialized && Bass.LastError != Errors.Already)
            {
                return null;
            }

            Bass.CurrentRecordingDevice = deviceId;
            try
            {
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
                return Bass.RecordGetInfo(out var recordInfo) ? Math.Max(recordInfo.Channels, 1) : null;
#else
                int devicePeriod = Bass.GetConfig(Configuration.DevicePeriod);
                foreach ((int channels, int rate) in PROBE_CONFIGS)
                {
                    using var probe = new BassMicChannelProbe(channels);
                    int handle = Bass.RecordStart(rate, channels, BassFlags.Default, devicePeriod, probe.ReceiveFrame,
                        IntPtr.Zero);

                    if (handle == 0)
                    {
                        continue;
                    }

                    int detectedChannelCount;
                    try
                    {
                        detectedChannelCount = probe.CountActiveChannels();
                    }
                    finally
                    {
                        Bass.ChannelStop(handle);
                    }

                    if (detectedChannelCount == 0)
                    {
                        continue;
                    }

                    if (channels == 8 && detectedChannelCount < 3)
                    {
                        continue;
                    }

                    return detectedChannelCount;
                }
#endif
            }
            finally
            {
                if (initialized)
                {
                    Bass.RecordFree();
                }
            }

#if !UNITY_EDITOR_OSX && !UNITY_STANDALONE_OSX
            YargLogger.LogTrace($"Channel probe: no usable frame from [{deviceId}] '{name}'");
            return null;
#endif
        }

        private int CountActiveChannels()
        {
            int deadline = Environment.TickCount + TIMEOUT_MS;
            while (true)
            {
                int remaining = deadline - Environment.TickCount;
                if (remaining <= 0 || !_frameReceived.Wait(remaining))
                {
                    return 0;
                }

                _frameReceived.Reset();

                if (_latestFrame.Length == 0 || IsSilent(_latestFrame))
                {
                    continue;
                }

                int sampleCountPerChannel = _latestFrame.Length / _reportedChannelCount;
                if (sampleCountPerChannel == 0)
                {
                    return 0;
                }

                short[][] channelSamples = Deinterleave(_latestFrame, _reportedChannelCount, sampleCountPerChannel);

                int lastActiveChannel = -1;
                for (int channel = 0; channel < _reportedChannelCount; channel++)
                {
                    if (!IsSilent(channelSamples[channel]) && !IsDuplicate(channelSamples, channel))
                    {
                        lastActiveChannel = channel;
                    }
                }

                return lastActiveChannel + 1;
            }
        }

        private bool ReceiveFrame(int handle, IntPtr buffer, int length, IntPtr user)
        {
            if (length <= 0)
            {
                return true;
            }

            unsafe
            {
                var samples = new Span<short>((short*) buffer, length / sizeof(short));
                _latestFrame = samples.ToArray();
                _frameReceived.Set();
            }

            return true;
        }

        private static short[][] Deinterleave(short[] interleaved, int channelCount, int samplesPerChannel)
        {
            short[][] channelSamples = new short[channelCount][];
            for (int channel = 0; channel < channelCount; channel++)
            {
                short[] samples = new short[samplesPerChannel];
                for (int sample = 0; sample < samplesPerChannel; sample++)
                {
                    samples[sample] = interleaved[sample * channelCount + channel];
                }

                channelSamples[channel] = samples;
            }

            return channelSamples;
        }

        private static bool IsSilent(short[] samples) => Array.TrueForAll(samples, sample => sample == 0);

        private static bool IsDuplicate(short[][] channels, int channel)
        {
            for (int previousChannel = 0; previousChannel < channel; previousChannel++)
            {
                if (ChannelsEquivalent(channels[channel], channels[previousChannel]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ChannelsEquivalent(short[] first, short[] second)
        {
            int maximumDifferences = first.Length / 100;
            int differences = 0;
            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i] && ++differences > maximumDifferences)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
