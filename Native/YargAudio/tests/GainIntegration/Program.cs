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
        Check(GainDsp.GetAbiVersion() == 1, "Unexpected YargAudio ABI version.");
        Check(Bass.Init(0, 48_000, 0, IntPtr.Zero, IntPtr.Zero), "BASS_Init");

        try
        {
            TestParityAndLiveUpdates();
            TestDspPriority();
            TestRepeatedLifecycle();
            TestFreeverbImpulseAndReset();
        }
        finally
        {
            Check(Bass.Free(), "BASS_Free");
        }

        Console.WriteLine($"Native Gain/Freeverb integration passed on {RuntimeInformation.OSDescription} " +
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

}
