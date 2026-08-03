#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a Gain DSP implemented and attached entirely by the YargAudio native plugin.
    /// The BASS channel passed to <see cref="Attach"/> must outlive this handle.
    /// </summary>
    internal sealed class BassGainDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const uint ABI_VERSION = 1;
        private const string EFFECT_NAME = "native Gain DSP";

        private BassGainDsp() : base(true)
        {
        }

        internal static BassGainDsp? Attach(int channelHandle, float initialGain, int priority = 0)
        {
            if (channelHandle == 0 || !IsFinite(initialGain))
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, gain={2}, priority={3}.",
                    EFFECT_NAME, channelHandle, initialGain, priority);
                return null;
            }

            try
            {
                uint nativeVersion = Native.GetAbiVersion();
                if (nativeVersion != ABI_VERSION)
                {
                    YargLogger.LogError(
                        $"Cannot attach {EFFECT_NAME}: ABI mismatch managed={ABI_VERSION}, " +
                        $"native={nativeVersion}, channel={channelHandle}, " +
                        $"platform={PlatformDescription}.");
                    return null;
                }

                int result = Native.Attach(unchecked((uint) channelHandle), initialGain, priority,
                    out BassGainDsp dsp, out int bassError);
                if (result == 0 && dsp != null && !dsp.IsInvalid)
                {
                    return dsp;
                }

                // Native initializes the output to null on failure. Dispose any unexpected handle
                // so a partially successful future implementation cannot leak through this path.
                dsp?.Dispose();
                YargLogger.LogError(
                    $"Failed to attach {EFFECT_NAME}: result={result}, BASS={bassError}, " +
                    $"channel={channelHandle}, gain={initialGain}, priority={priority}, " +
                    $"platform={PlatformDescription}.");
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception,
                    $"Failed to load {EFFECT_NAME} for channel {channelHandle} " +
                    $"on {PlatformDescription}");
                return null;
            }
        }

        internal bool SetGain(float gain)
        {
            if (!IsFinite(gain))
            {
                YargLogger.LogFormatError("Ignoring non-finite gain for {0}: {1}.",
                    EFFECT_NAME, gain);
                return false;
            }

            if (IsClosed || IsInvalid)
            {
                return false;
            }

            try
            {
                // SafeHandle marshaling keeps native state alive if SetGain races disposal.
                return Native.SetGain(this, gain) == 0;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        protected override bool ReleaseHandle()
        {
            Native.Destroy(handle);
            return true;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        // Thread-safe .NET equivalents of Unity's Application.platform/SystemInfo.processorType.
        // Attach runs from background threads (e.g. music player audio load), where Unity APIs throw.
        private static string PlatformDescription =>
            $"{RuntimeInformation.OSDescription}/{RuntimeInformation.ProcessArchitecture}/{IntPtr.Size * 8}-bit";

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_audio_get_abi_version",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetAbiVersion();

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(uint channel, float gain, int priority,
                out BassGainDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_set_gain",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetGain(BassGainDsp dsp, float gain);

            [DllImport(LIBRARY, EntryPoint = "yarg_gain_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Destroy(IntPtr dsp);
        }
    }
}
