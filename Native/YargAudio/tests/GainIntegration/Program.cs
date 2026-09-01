using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
    private const uint BassSampleFloat = 0x100;
    private const uint BassStreamDecode = 0x200000;
    private static readonly IntPtr StreamProcPush = new(-1);

    private static int Main()
    {
        Check(Environment.Is64BitProcess, "Integration probe requires a 64-bit process.");
        Check(GainDsp.GetAbiVersion() == 8, "Unexpected YargAudio ABI version.");
        Check(Bass.Init(0, 48_000, 0, IntPtr.Zero, IntPtr.Zero), "BASS_Init");

        try
        {
            TestParityAndLiveUpdates();
            TestDspPriority();
            TestRepeatedLifecycle();
            TestNoiseGateAndReset();
            TestFreeverbImpulseAndReset();
            TestDattorroImpulseAndReset();
            TestOneShotRealBassLifecycle();
            TestReadAheadRealBassGraph();
        }
        finally
        {
            Check(Bass.Free(), "BASS_Free");
        }

        Console.WriteLine($"Native DSP integration passed on {RuntimeInformation.OSDescription} " +
            $"({RuntimeInformation.ProcessArchitecture}).");
        return 0;
    }

    private static void TestParityAndLiveUpdates()
    {
        uint stream = CreatePushStream();
        try
        {
            using GainDsp dsp = Attach(stream, 1);
            float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int) 0x80000000));
            float[] input = { negativeZero, 0, -2, -0.5f, 0.25f, 0.5f, 1, 2 };

            foreach (float gain in new[] { 1, 0, 2, -0.5f })
            {
                Check(GainDsp.SetGain(dsp, gain) == 0, $"SetGain({gain})");
                float[] output = Process(stream, input);
                for (int i = 0; i < input.Length; i++)
                {
                    float expected = input[i] * gain;
                    Check(BitConverter.SingleToInt32Bits(output[i]) ==
                        BitConverter.SingleToInt32Bits(expected),
                        $"Parity mismatch: gain={gain}, sample={i}, " +
                        $"expected={expected}, actual={output[i]}.");
                }
            }
        }
        finally
        {
            Check(Bass.StreamFree(stream), "BASS_StreamFree after parity test");
        }
    }

    private static void TestDspPriority()
    {
        uint stream = CreatePushStream();
        Bass.DspProcedure addOne = (_, _, buffer, length, _) =>
        {
            if (buffer == IntPtr.Zero || length == 0)
            {
                return;
            }

            unsafe
            {
                float* samples = (float*) buffer;
                for (int i = 0; i < length / sizeof(float); i++)
                {
                    samples[i] += 1;
                }
            }
        };

        try
        {
            // BASS invokes higher-priority DSPs first: native Gain, then test callback.
            using GainDsp dsp = Attach(stream, 2, priority: 10);
            uint observer = Bass.ChannelSetDsp(stream, addOne, IntPtr.Zero, priority: 0);
            Check(observer != 0, "BASS_ChannelSetDSP priority observer");
            try
            {
                float[] input = { -1, 0, 0.5f, 2 };
                float[] output = Process(stream, input);
                for (int i = 0; i < input.Length; i++)
                {
                    Check(output[i] == input[i] * 2 + 1,
                        $"DSP priority mismatch at sample {i}.");
                }
            }
            finally
            {
                Check(Bass.ChannelRemoveDsp(stream, observer),
                    "BASS_ChannelRemoveDSP priority observer");
                GC.KeepAlive(addOne);
            }
        }
        finally
        {
            Check(Bass.StreamFree(stream), "BASS_StreamFree after priority test");
        }
    }

    private static void TestRepeatedLifecycle()
    {
        uint stream = CreatePushStream();
        try
        {
            for (int i = 0; i < 1_000; i++)
            {
                using GainDsp dsp = Attach(stream, 1);
                float gain = i % 3 switch
                {
                    0 => 0,
                    1 => -1,
                    _ => 2,
                };
                Check(GainDsp.SetGain(dsp, gain) == 0,
                    $"SetGain during lifecycle iteration {i}");
            }
        }
        finally
        {
            // Every SafeHandle is explicitly disposed before parent stream destruction.
            Check(Bass.StreamFree(stream), "BASS_StreamFree after lifecycle test");
        }
    }

    private static void TestFreeverbImpulseAndReset()
    {
        uint stream = CreatePushStream();
        try
        {
            using FreeverbDsp dsp = AttachFreeverb(stream, 0, 1, 0.8f, 0.5f, 1);
            float[] impulse = new float[2_000 * 2];
            impulse[0] = 1;
            float[] output = Process(stream, impulse);
            bool producedWetSignal = false;
            foreach (float sample in output)
            {
                if (sample != 0)
                {
                    producedWetSignal = true;
                    break;
                }
            }
            Check(producedWetSignal, "Freeverb impulse response");

            Check(FreeverbDsp.Reset(dsp) == 0, "Freeverb reset");
            float[] silence = new float[64 * 2];
            output = Process(stream, silence);
            foreach (float sample in output)
            {
                Check(sample == 0, "Freeverb reset left tail");
            }
        }
        finally
        {
            Check(Bass.StreamFree(stream), "BASS_StreamFree after Freeverb test");
        }
    }

    private static void TestDattorroImpulseAndReset()
    {
        uint stream = CreatePushStream();
        try
        {
            using DattorroDsp dsp = AttachDattorro(stream, 0, 1, 0.8f, 0.5f, 1);
            float[] impulse = new float[2_000 * 2];
            impulse[0] = 1;
            float[] output = Process(stream, impulse);
            bool producedWetSignal = false;
            foreach (float sample in output)
            {
                if (sample != 0)
                {
                    producedWetSignal = true;
                    break;
                }
            }
            Check(producedWetSignal, "Dattorro impulse response");

            Check(DattorroDsp.Reset(dsp) == 0, "Dattorro reset");
            float[] silence = new float[64 * 2];
            output = Process(stream, silence);
            foreach (float sample in output)
            {
                Check(sample == 0, "Dattorro reset left tail");
            }
        }
        finally
        {
            Check(Bass.StreamFree(stream), "BASS_StreamFree after Dattorro test");
        }
    }

    private static void TestNoiseGateAndReset()
    {
        uint stream = CreatePushStream();
        try
        {
            using NoiseGateDsp gate = AttachNoiseGate(stream, 0.05f, 0, 0, 0, 0, priority: 5);

            float[] output = Process(stream, new float[] { 0, 0, 0, 0 });
            AssertSilence(output, "noise gate silence");

            output = Process(stream, new float[] { 1, 1 });
            Check(output[0] == 1 && output[1] == 1, "noise gate loud signal");

            output = Process(stream, new float[] { 0.01f, 0.01f });
            AssertSilence(output, "noise gate quiet signal");

            Check(NoiseGateDsp.Reset(gate) == 0, "noise gate reset");
            output = Process(stream, new float[] { 0, 0, 0, 0 });
            AssertSilence(output, "noise gate reset silence");
        }
        finally
        {
            Check(Bass.StreamFree(stream), "BASS_StreamFree after dynamics test");
        }
    }

    private static void TestOneShotRealBassLifecycle()
    {
        const int sampleRate = 48_000;
        const int channels = 2;
        float[] pcm = { 1, -1, 0.5f, -0.5f };
        double[] schedule = { 0 };

        uint mixer = CreateMixer(sampleRate, channels);
        try
        {
            using OneShotStream stream = CreateOneShot(sampleRate, channels, pcm, schedule);
            Check(stream.Attach(mixer, 0, 1, paused: false), "one-shot attach");
            AssertOutput(Pull(mixer, 4), pcm, "one-shot initial render");

            Check(stream.SetGain(0), "one-shot mute");
            Check(stream.Resync(mixer, 0, 1, clearActiveVoices: true),
                "one-shot resync while muted");
            AssertSilence(Pull(mixer, 4), "one-shot muted render");

            Check(stream.SetGain(1), "one-shot unity gain");
            Check(stream.SetPaused(mixer, true), "one-shot pause");
            Check(stream.Resync(mixer, 0, 1, clearActiveVoices: true),
                "one-shot resync while paused");
            AssertSilence(Pull(mixer, 4), "one-shot paused render");
            Check(stream.SetPaused(mixer, false), "one-shot resume");
            AssertOutput(Pull(mixer, 4), pcm, "one-shot resumed render");

            uint replacement = CreateMixer(sampleRate, channels);
            try
            {
                Check(stream.Detach(), "one-shot detach");
                Check(stream.Attach(replacement, 0, 1, paused: false), "one-shot reattach");
                AssertOutput(Pull(replacement, 4), pcm, "one-shot replacement render");
                Check(stream.Detach(), "one-shot replacement detach");
            }
            finally
            {
                Check(Bass.StreamFree(replacement), "BASS_StreamFree replacement mixer");
            }
        }
        finally
        {
            Check(Bass.StreamFree(mixer), "BASS_StreamFree one-shot mixer");
        }
    }

    private static uint CreateMixer(int sampleRate, int channels)
    {
        uint mixer = Bass.MixerStreamCreate((uint) sampleRate, (uint) channels,
            BassSampleFloat | BassStreamDecode);
        Check(mixer != 0, "BASS_Mixer_StreamCreate");
        return mixer;
    }

    private static void TestReadAheadRealBassGraph()
    {
        const int sampleRate = 48_000;
        const int channels = 2;
        uint source = CreatePushStream();
        uint mixer = CreateMixer(sampleRate, channels);
        try
        {
            Check(Bass.MixerStreamAddChannel(mixer, source, 0x800000),
                "BASS_Mixer_StreamAddChannel read-ahead source");
            float[] input = new float[512 * channels];
            Array.Fill(input, 0.25f);
            Check(Bass.StreamPutData(source, input, input.Length * sizeof(float)) ==
                input.Length * sizeof(float), "BASS_StreamPutData read-ahead source");

            var config = new ReadAheadConfig
            {
                Size = (uint) Marshal.SizeOf<ReadAheadConfig>(),
                BassDeviceId = 0,
                SourceMixer = mixer,
                SampleRate = sampleRate,
                Channels = channels,
                MinimumBlockFrames = 64,
                BufferMilliseconds = 2,
            };
            using ReadAheadStream stream = ReadAheadStream.Create(config);
            uint finalMixer = CreateMixer(sampleRate, channels);
            try
            {
                Check(Bass.MixerStreamAddChannel(finalMixer, stream.StreamHandle, 0x800000),
                    "BASS_Mixer_StreamAddChannel read-ahead stream");
                Check(stream.Prefill(2000), "read-ahead prefill");
                float[] output = Pull(finalMixer, 96);
                foreach (float sample in output)
                {
                    Check(sample == 0.25f, "read-ahead PCM mismatch");
                }
                Check(stream.Flush(), "read-ahead flush");
                Check(stream.SetBufferLength(4), "read-ahead resize");
                Check(stream.Prefill(2000), "read-ahead refill");
            }
            finally
            {
                Check(Bass.StreamFree(finalMixer), "BASS_StreamFree final read-ahead mixer");
            }
        }
        finally
        {
            Check(Bass.StreamFree(mixer), "BASS_StreamFree read-ahead mixer");
            Check(Bass.StreamFree(source), "BASS_StreamFree read-ahead source");
        }
    }

    private static OneShotStream CreateOneShot(int sampleRate, int channels,
        float[] pcm, double[] schedule)
    {
        var config = new OneShotConfig
        {
            Size = (uint) Marshal.SizeOf<OneShotConfig>(),
            SampleRate = (uint) sampleRate,
            Channels = (uint) channels,
            LeadTime = 0,
        };

        int result = OneShotStream.Create(ref config, pcm, (ulong) pcm.LongLength,
            schedule, (ulong) schedule.LongLength, out OneShotStream stream,
            out int bassError);
        Check(result == 0 && stream != null && !stream.IsInvalid,
            $"one-shot create failed: result={result}, BASS={bassError}.");
        return stream!;
    }

    private static float[] Pull(uint mixer, int frames)
    {
        float[] output = new float[frames * 2];
        int bytes = output.Length * sizeof(float);
        Check(Bass.ChannelGetData(mixer, output, bytes) == bytes,
            "BASS_ChannelGetData one-shot mixer");
        return output;
    }

    private static void AssertOutput(float[] actual, float[] expected, string operation)
    {
        for (int i = 0; i < expected.Length; i++)
        {
            Check(actual[i] == expected[i],
                $"{operation}: sample {i}, expected {expected[i]}, actual {actual[i]}.");
        }

        for (int i = expected.Length; i < actual.Length; i++)
        {
            Check(actual[i] == 0, $"{operation}: trailing sample {i} was {actual[i]}.");
        }
    }

    private static void AssertSilence(float[] actual, string operation)
    {
        for (int i = 0; i < actual.Length; i++)
        {
            Check(actual[i] == 0, $"{operation}: sample {i} was {actual[i]}.");
        }
    }

    private static uint CreatePushStream()
    {
        uint stream = Bass.StreamCreate(48_000, 2, BassSampleFloat | BassStreamDecode,
            StreamProcPush, IntPtr.Zero);
        Check(stream != 0, "BASS_StreamCreate");
        return stream;
    }

    private static GainDsp Attach(uint stream, float gain, int priority = 0)
    {
        int result = GainDsp.Attach(stream, gain, priority, out GainDsp dsp,
            out int bassError);
        Check(result == 0 && dsp != null && !dsp.IsInvalid,
            $"Gain attach failed: result={result}, BASS={bassError}.");
        return dsp!;
    }

    private static FreeverbDsp AttachFreeverb(uint stream, float dryMix, float wetMix,
        float roomSize, float damp, float width, int priority = 0)
    {
        int result = FreeverbDsp.Attach(stream, dryMix, wetMix, roomSize, damp, width,
            priority, out FreeverbDsp dsp, out int bassError);
        Check(result == 0 && dsp != null && !dsp.IsInvalid,
            $"Freeverb attach failed: result={result}, BASS={bassError}.");
        return dsp!;
    }

    private static DattorroDsp AttachDattorro(uint stream, float dryMix, float wetMix,
        float roomSize, float damp, float width, int priority = 0)
    {
        int result = DattorroDsp.Attach(stream, dryMix, wetMix, roomSize, damp, width,
            priority, out DattorroDsp dsp, out int bassError);
        Check(result == 0 && dsp != null && !dsp.IsInvalid,
            $"Dattorro attach failed: result={result}, BASS={bassError}.");
        return dsp!;
    }

    private static NoiseGateDsp AttachNoiseGate(uint stream, float threshold,
        float floorGain, float attackMs, float holdMs, float releaseMs, int priority = 0)
    {
        int result = NoiseGateDsp.Attach(stream, threshold, floorGain, attackMs, holdMs,
            releaseMs, priority, out NoiseGateDsp dsp, out int bassError);
        Check(result == 0 && dsp != null && !dsp.IsInvalid,
            $"Noise gate attach failed: result={result}, BASS={bassError}.");
        return dsp!;
    }

    private static float[] Process(uint stream, float[] input)
    {
        int byteLength = input.Length * sizeof(float);
        Check(Bass.StreamPutData(stream, input, byteLength) == byteLength,
            "BASS_StreamPutData");

        float[] output = new float[input.Length];
        Check(Bass.ChannelGetData(stream, output, byteLength) == byteLength,
            "BASS_ChannelGetData");
        return output;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"{message} BASS={Bass.ErrorGetCode()}.");
        }
    }

    private sealed class GainDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private GainDsp() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Destroy(handle);
            return true;
        }

        [DllImport("yarg_audio", EntryPoint = "yarg_audio_get_abi_version",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern uint GetAbiVersion();

        [DllImport("yarg_audio", EntryPoint = "yarg_gain_dsp_attach",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Attach(uint channel, float gain, int priority,
            out GainDsp dsp, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_gain_dsp_set_gain",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int SetGain(GainDsp dsp, float gain);

        [DllImport("yarg_audio", EntryPoint = "yarg_gain_dsp_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void Destroy(IntPtr dsp);

    }

    private sealed class FreeverbDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private FreeverbDsp() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Destroy(handle);
            return true;
        }

        [DllImport("yarg_audio", EntryPoint = "yarg_freeverb_dsp_attach",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Attach(uint channel, float dryMix, float wetMix,
            float roomSize, float damp, float width, int priority,
            out FreeverbDsp dsp, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_freeverb_dsp_reset",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Reset(FreeverbDsp dsp);

        [DllImport("yarg_audio", EntryPoint = "yarg_freeverb_dsp_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void Destroy(IntPtr dsp);
    }

    private sealed class DattorroDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private DattorroDsp() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Destroy(handle);
            return true;
        }

        [DllImport("yarg_audio", EntryPoint = "yarg_dattorro_reverb_dsp_attach",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Attach(uint channel, float dryMix, float wetMix,
            float roomSize, float damp, float width, int priority,
            out DattorroDsp dsp, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_dattorro_reverb_dsp_reset",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Reset(DattorroDsp dsp);

        [DllImport("yarg_audio", EntryPoint = "yarg_dattorro_reverb_dsp_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void Destroy(IntPtr dsp);
    }

    private sealed class NoiseGateDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private NoiseGateDsp() : base(true)
        {
        }

        protected override bool ReleaseHandle()
        {
            Destroy(handle);
            return true;
        }

        [DllImport("yarg_audio", EntryPoint = "yarg_noise_gate_dsp_attach",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Attach(uint channel, float threshold, float floorGain,
            float attackMs, float holdMs, float releaseMs, int priority,
            out NoiseGateDsp dsp, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_noise_gate_dsp_reset",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Reset(NoiseGateDsp dsp);

        [DllImport("yarg_audio", EntryPoint = "yarg_noise_gate_dsp_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern void Destroy(IntPtr dsp);
    }

    private static class Bass
    {
        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        internal delegate void DspProcedure(uint dsp, uint channel, IntPtr buffer,
            uint length, IntPtr user);

        [DllImport("bass", EntryPoint = "BASS_Init")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Init(int device, uint frequency, uint flags,
            IntPtr window, IntPtr dsguid);

        [DllImport("bass", EntryPoint = "BASS_Free")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool Free();

        [DllImport("bass", EntryPoint = "BASS_ErrorGetCode")]
        internal static extern int ErrorGetCode();

        [DllImport("bass", EntryPoint = "BASS_StreamCreate")]
        internal static extern uint StreamCreate(uint frequency, uint channels, uint flags,
            IntPtr procedure, IntPtr user);

        [DllImport("bass", EntryPoint = "BASS_StreamPutData")]
        internal static extern int StreamPutData(uint stream, float[] buffer, int length);

        [DllImport("bass", EntryPoint = "BASS_ChannelGetData")]
        internal static extern int ChannelGetData(uint channel, float[] buffer, int length);

        [DllImport("bassmix", EntryPoint = "BASS_Mixer_StreamCreate")]
        internal static extern uint MixerStreamCreate(uint frequency, uint channels,
            uint flags);

        [DllImport("bassmix", EntryPoint = "BASS_Mixer_StreamAddChannel")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool MixerStreamAddChannel(uint mixer, uint channel,
            uint flags);

        [DllImport("bass", EntryPoint = "BASS_ChannelSetDSP")]
        internal static extern uint ChannelSetDsp(uint channel, DspProcedure procedure,
            IntPtr user, int priority);

        [DllImport("bass", EntryPoint = "BASS_ChannelRemoveDSP")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ChannelRemoveDsp(uint channel, uint dsp);

        [DllImport("bass", EntryPoint = "BASS_StreamFree")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool StreamFree(uint stream);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct OneShotConfig
    {
        internal uint Size;
        internal uint SampleRate;
        internal uint Channels;
        internal uint Reserved;
        internal double LeadTime;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ReadAheadConfig
    {
        internal uint Size;
        internal int BassDeviceId;
        internal uint SourceMixer;
        internal uint SampleRate;
        internal uint Channels;
        internal uint MinimumBlockFrames;
        internal uint BufferMilliseconds;
    }

    private sealed class ReadAheadStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal uint StreamHandle { get; private set; }

        private ReadAheadStream() : base(true) { }

        internal static ReadAheadStream Create(ReadAheadConfig config)
        {
            int result = CreateNative(in config, out ReadAheadStream stream,
                out uint streamHandle, out int bassError);
            Check(result == 0 && stream != null && !stream.IsInvalid && streamHandle != 0,
                $"read-ahead create failed: result={result}, BASS={bassError}.");
            stream!.StreamHandle = streamHandle;
            return stream;
        }

        internal bool Prefill(uint timeoutMilliseconds) =>
            PrefillNative(this, timeoutMilliseconds) == 0;

        internal bool Flush() => FlushNative(this) == 0;

        internal bool SetBufferLength(uint milliseconds) =>
            SetBufferLengthNative(this, milliseconds) == 0;

        protected override bool ReleaseHandle() => Destroy(handle, out _) == 0;

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_create",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int CreateNative(in ReadAheadConfig config,
            out ReadAheadStream stream, out uint streamHandle, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_prefill",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int PrefillNative(ReadAheadStream stream,
            uint timeoutMilliseconds);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_flush",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int FlushNative(ReadAheadStream stream);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_set_buffer_length",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetBufferLengthNative(ReadAheadStream stream,
            uint milliseconds);

        [DllImport("yarg_audio", EntryPoint = "yarg_read_ahead_stream_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int Destroy(IntPtr stream, out int bassError);
    }

    private sealed class OneShotStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        private OneShotStream() : base(true) { }

        protected override bool ReleaseHandle() => Destroy(handle, out _) == 0;

        internal bool Attach(uint mixer, double anchorSongPosition,
            float playbackSpeed, bool paused) => AttachNative(this, mixer,
            anchorSongPosition, playbackSpeed, paused ? 1 : 0, out _) == 0;

        internal bool Resync(uint mixer, double anchorSongPosition, float playbackSpeed,
            bool clearActiveVoices) => ResyncNative(this, mixer, anchorSongPosition,
            playbackSpeed, clearActiveVoices ? 1 : 0, out _) == 0;

        internal bool SetPaused(uint mixer, bool paused) =>
            SetPausedNative(this, mixer, paused ? 1 : 0, out _) == 0;

        internal bool SetGain(float gain) => SetGainNative(this, gain) == 0;
        internal bool Detach() => DetachNative(this, out _) == 0;

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_create",
            CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Create(ref OneShotConfig config, [In] float[] pcm,
            ulong pcmSampleCount, [In] double[] schedule, ulong scheduleCount,
            out OneShotStream stream, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_attach",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int AttachNative(OneShotStream stream, uint mixer,
            double anchorSongPosition, float playbackSpeed, int paused, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_resync_ex",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int ResyncNative(OneShotStream stream, uint mixer,
            double anchorSongPosition, float playbackSpeed, int clearActiveVoices,
            out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_set_paused",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetPausedNative(OneShotStream stream, uint mixer,
            int paused, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_set_gain",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int SetGainNative(OneShotStream stream, float gain);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_detach",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int DetachNative(OneShotStream stream, out int bassError);

        [DllImport("yarg_audio", EntryPoint = "yarg_one_shot_stream_destroy",
            CallingConvention = CallingConvention.Cdecl)]
        private static extern int Destroy(IntPtr stream, out int bassError);
    }

}
