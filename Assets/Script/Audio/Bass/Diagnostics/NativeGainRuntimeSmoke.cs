#if YARG_NATIVE_GAIN_SMOKE
#nullable enable
using System;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using YARG.Audio.BASS.Effects;

namespace YARG.Audio.BASS.Diagnostics
{
    internal static class NativeGainRuntimeSmoke
    {
        private const uint BASS_SAMPLE_FLOAT = 0x100;
        private const uint BASS_STREAM_DECODE = 0x200000;
        private static readonly IntPtr STREAMPROC_PUSH = new IntPtr(-1);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Run()
        {
            string? resultPath = GetArgument("-nativeGainSmokeResult");
            if (string.IsNullOrEmpty(resultPath))
            {
                return;
            }

            int exitCode = 0;
            string result;
            try
            {
                Execute();
                result = $"PASS backend={Backend} platform={Application.platform} " +
                    $"architecture={SystemInfo.processorType}";
                Debug.Log($"Native Gain runtime smoke passed: {result}");
            }
            catch (Exception exception)
            {
                exitCode = 1;
                result = $"FAIL backend={Backend} platform={Application.platform} " +
                    $"architecture={SystemInfo.processorType}\n{exception}";
                Debug.LogException(exception);
            }

            try
            {
                string? directory = Path.GetDirectoryName(resultPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                File.WriteAllText(resultPath, result + Environment.NewLine);
            }
            catch (Exception exception)
            {
                exitCode = 1;
                Debug.LogException(exception);
            }

            Application.Quit(exitCode);
        }

        private static void Execute()
        {
            Check(IntPtr.Size == 8, "Smoke player must be 64-bit.");
            Check(Bass.Init(0, 48_000, 0, IntPtr.Zero, IntPtr.Zero), "BASS_Init");

            try
            {
                TestParityAndForcedCollections();
                TestRepeatedLifecycle();
            }
            finally
            {
                Check(Bass.Free(), "BASS_Free");
            }
        }

        private static void TestParityAndForcedCollections()
        {
            uint stream = CreatePushStream();
            try
            {
                using BassGainDsp dsp = Attach(stream, 1f);
                float negativeZero = BitConverter.Int32BitsToSingle(unchecked((int) 0x80000000));
                float[] input = { negativeZero, 0f, -2f, -0.5f, 0.25f, 0.5f, 1f, 2f };

                for (int iteration = 0; iteration < 32; iteration++)
                {
                    float gain = iteration % 4 switch
                    {
                        0 => 1f,
                        1 => 0f,
                        2 => 2f,
                        _ => -0.5f,
                    };
                    Check(dsp.SetGain(gain), $"SetGain({gain})");

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();

                    float[] output = Process(stream, input);
                    for (int i = 0; i < input.Length; i++)
                    {
                        float expected = input[i] * gain;
                        Check(BitConverter.SingleToInt32Bits(output[i]) ==
                            BitConverter.SingleToInt32Bits(expected),
                            $"Parity mismatch: iteration={iteration}, gain={gain}, sample={i}, " +
                            $"expected={expected}, actual={output[i]}.");
                    }
                }
            }
            finally
            {
                Check(Bass.StreamFree(stream), "BASS_StreamFree after parity test");
            }
        }

        private static void TestRepeatedLifecycle()
        {
            uint stream = CreatePushStream();
            try
            {
                for (int i = 0; i < 256; i++)
                {
                    using BassGainDsp dsp = Attach(stream, 1f);
                    Check(dsp.SetGain((i % 3) - 1f),
                        $"SetGain during lifecycle iteration {i}");
                }
            }
            finally
            {
                Check(Bass.StreamFree(stream), "BASS_StreamFree after lifecycle test");
            }
        }

        private static uint CreatePushStream()
        {
            uint stream = Bass.StreamCreate(48_000, 2, BASS_SAMPLE_FLOAT | BASS_STREAM_DECODE,
                STREAMPROC_PUSH, IntPtr.Zero);
            Check(stream != 0, "BASS_StreamCreate");
            return stream;
        }

        private static BassGainDsp Attach(uint stream, float gain)
        {
            BassGainDsp? dsp = BassGainDsp.Attach(unchecked((int) stream), gain);
            Check(dsp != null && !dsp.IsInvalid, "BassGainDsp.Attach");
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

        private static void Check(bool condition, string operation)
        {
            if (!condition)
            {
                throw new InvalidOperationException($"{operation} failed; BASS={Bass.ErrorGetCode()}.");
            }
        }

        private static string? GetArgument(string name)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i < arguments.Length - 1; i++)
            {
                if (string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return arguments[i + 1];
                }
            }
            return null;
        }

        private static string Backend
        {
            get
            {
#if ENABLE_IL2CPP
                return "IL2CPP";
#else
                return "Mono";
#endif
            }
        }

        private static class Bass
        {
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

            [DllImport("bass", EntryPoint = "BASS_StreamFree")]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool StreamFree(uint stream);
        }
    }
}
#endif
