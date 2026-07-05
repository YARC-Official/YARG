using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedBass;
using ManagedBass.Fx;
using ManagedBass.Mix;

internal static class Program
{
    private const int SampleRate = 44100;
    private const int Channels = 2;

    private static unsafe int Main(string[] args)
    {
        int bufferMs = GetIntArg(args, "--buffer-ms", 5000);
        int durationSec = GetIntArg(args, "--duration-sec", 20);
        bool toggleTempo = HasArg(args, "--toggle-tempo");
        bool muted = HasArg(args, "--muted");

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            Console.Error.WriteLine("BassPositionTest expects Windows x64 native BASS DLLs.");
            return 1;
        }

        Bass.Configure(Configuration.IncludeDefaultDevice, true);
        Bass.UpdatePeriod = 5;
        Bass.PlaybackBufferLength = Math.Max(bufferMs, 5000);
        Bass.DeviceNonStop = true;
        Bass.FloatingPointDSP = true;

        if (!Bass.Init(-1, SampleRate, DeviceInitFlags.Default | DeviceInitFlags.Latency, IntPtr.Zero))
        {
            Console.Error.WriteLine($"Bass.Init failed: {Bass.LastError}");
            return 1;
        }

        SineGenerator generator = new(SampleRate, Channels, muted ? 0f : 0.02f);
        StreamProcedure streamProcedure = generator.Write;
        int source = 0;
        int mixer = 0;
        int tempo = 0;
        OutputBufferTracker? tracker = null;

        try
        {
            source = Bass.CreateStream(SampleRate, Channels, BassFlags.Decode | BassFlags.Float, streamProcedure, IntPtr.Zero);
            if (source == 0)
            {
                return Fail("Create source", Bass.LastError);
            }

            mixer = BassMix.CreateMixerStream(SampleRate, Channels, BassFlags.Decode | BassFlags.Float);
            if (mixer == 0)
            {
                return Fail("Create mixer", Bass.LastError);
            }

            if (!BassMix.MixerAddChannel(mixer, source, BassFlags.Default))
            {
                return Fail("Add source to mixer", Bass.LastError);
            }

            tempo = BassFx.TempoCreate(mixer, BassFlags.Default | BassFlags.SampleOverrideLowestVolume);
            if (tempo == 0)
            {
                return Fail("Create tempo stream", Bass.LastError);
            }

            float bufferSeconds = Math.Max(0, bufferMs) / 1000f;
            if (!Bass.ChannelSetAttribute(tempo, ChannelAttribute.Buffer, bufferSeconds))
            {
                return Fail("Set tempo buffer", Bass.LastError);
            }

            tracker = new OutputBufferTracker(tempo);
            if (!tracker.Installed)
            {
                return Fail("Install DSP tracker", Bass.LastError);
            }

            if (!Bass.ChannelPlay(tempo))
            {
                return Fail("Play tempo stream", Bass.LastError);
            }

            Console.WriteLine($"bufferMs={bufferMs} durationSec={durationSec} updatePeriodMs={Bass.UpdatePeriod} toggleTempo={toggleTempo} muted={muted}");
            Console.WriteLine("timeMs\tplayedSec\tplayedBytes\tproducedBytes\tremainingMs\tminMs\tmaxMs\ttempoPct");

            Stopwatch stopwatch = Stopwatch.StartNew();
            double minMs = double.PositiveInfinity;
            double maxMs = 0;
            int lastToggleSec = -1;
            float tempoPct = 0;

            while (stopwatch.Elapsed.TotalSeconds < durationSec)
            {
                if (toggleTempo)
                {
                    int elapsedSec = (int) stopwatch.Elapsed.TotalSeconds;
                    if (elapsedSec != lastToggleSec && elapsedSec > 0 && elapsedSec % 5 == 0)
                    {
                        lastToggleSec = elapsedSec;
                        tempoPct = tempoPct == 0 ? 5 : 0;
                        if (!Bass.ChannelSetAttribute(tempo, ChannelAttribute.Tempo, tempoPct))
                        {
                            Console.Error.WriteLine($"Set tempo failed: {Bass.LastError}");
                        }
                    }
                }

                long playedBytes = Bass.ChannelGetPosition(tempo, PositionFlags.Bytes);
                double playedSec = playedBytes >= 0 ? Bass.ChannelBytes2Seconds(tempo, playedBytes) : -1;
                double remainingMs = tracker.TryGetRemainingSeconds(out double remainingSec) ? remainingSec * 1000 : -1;

                if (remainingMs >= 0)
                {
                    minMs = Math.Min(minMs, remainingMs);
                    maxMs = Math.Max(maxMs, remainingMs);
                }

                Console.WriteLine(
                    $"{stopwatch.ElapsedMilliseconds}\t{playedSec:0.000}\t{playedBytes}\t{tracker.ProducedBytes}\t{remainingMs:0.000}\t{minMs:0.000}\t{maxMs:0.000}\t{tempoPct:0.0}");

                Thread.Sleep(250);
            }

            return 0;
        }
        finally
        {
            tracker?.Dispose();
            if (tempo != 0)
            {
                Bass.StreamFree(tempo);
            }
            if (mixer != 0)
            {
                Bass.StreamFree(mixer);
            }
            if (source != 0)
            {
                Bass.StreamFree(source);
            }
            Bass.Free();
        }
    }

    private static int Fail(string operation, Errors error)
    {
        Console.Error.WriteLine($"{operation} failed: {error}");
        return 1;
    }

    private static int GetIntArg(string[] args, string name, int fallback)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name && int.TryParse(args[i + 1], out int value))
            {
                return value;
            }
        }

        return fallback;
    }

    private static bool HasArg(string[] args, string name)
    {
        return args.Any(arg => arg == name);
    }

    private sealed class OutputBufferTracker : IDisposable
    {
        private readonly DSPProcedure _dspProcedure;
        private readonly int _channelHandle;
        private int _dspHandle;
        private long _basePlayedBytes;
        private long _producedBytesSinceReset;
        private int _hasSeenCallback;

        public bool Installed => _dspHandle != 0;
        public long ProducedBytes => Interlocked.Read(ref _producedBytesSinceReset);

        public OutputBufferTracker(int channelHandle)
        {
            _channelHandle = channelHandle;
            _dspProcedure = OnDsp;
            _dspHandle = Bass.ChannelSetDSP(channelHandle, _dspProcedure, IntPtr.Zero, 0);
            ResetToCurrentPosition();
        }

        public void ResetToCurrentPosition()
        {
            long playedBytes = Bass.ChannelGetPosition(_channelHandle, PositionFlags.Bytes);
            Interlocked.Exchange(ref _basePlayedBytes, Math.Max(0, playedBytes));
            Interlocked.Exchange(ref _producedBytesSinceReset, 0);
            Volatile.Write(ref _hasSeenCallback, 0);
        }

        public bool TryGetRemainingSeconds(out double seconds)
        {
            seconds = 0;
            if (_dspHandle == 0 || Volatile.Read(ref _hasSeenCallback) == 0)
            {
                return false;
            }

            long playedBytes = Bass.ChannelGetPosition(_channelHandle, PositionFlags.Bytes);
            if (playedBytes < 0)
            {
                return false;
            }

            long playedSinceReset = playedBytes - Interlocked.Read(ref _basePlayedBytes);
            if (playedSinceReset < 0)
            {
                return false;
            }

            long remainingBytes = Interlocked.Read(ref _producedBytesSinceReset) - playedSinceReset;
            if (remainingBytes < 0)
            {
                remainingBytes = 0;
            }

            double value = Bass.ChannelBytes2Seconds(_channelHandle, remainingBytes);
            if (value < 0 || double.IsNaN(value) || double.IsInfinity(value))
            {
                return false;
            }

            seconds = value;
            return true;
        }

        public void Dispose()
        {
            int dspHandle = Interlocked.Exchange(ref _dspHandle, 0);
            if (dspHandle != 0)
            {
                Bass.ChannelRemoveDSP(_channelHandle, dspHandle);
            }
        }

        private void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
        {
            Interlocked.Add(ref _producedBytesSinceReset, length);
            Volatile.Write(ref _hasSeenCallback, 1);
        }
    }

    private sealed class SineGenerator
    {
        private readonly int _sampleRate;
        private readonly int _channels;
        private readonly float _volume;
        private double _phase;

        public SineGenerator(int sampleRate, int channels, float volume)
        {
            _sampleRate = sampleRate;
            _channels = channels;
            _volume = volume;
        }

        public unsafe int Write(int handle, IntPtr buffer, int length, IntPtr user)
        {
            float* samples = (float*) buffer;
            int floatCount = length / sizeof(float);
            for (int i = 0; i < floatCount; i += _channels)
            {
                float value = (float) Math.Sin(_phase) * _volume;
                _phase += 2 * Math.PI * 440 / _sampleRate;
                if (_phase > 2 * Math.PI)
                {
                    _phase -= 2 * Math.PI;
                }

                for (int channel = 0; channel < _channels && i + channel < floatCount; channel++)
                {
                    samples[i + channel] = value;
                }
            }

            return length;
        }
    }
}
