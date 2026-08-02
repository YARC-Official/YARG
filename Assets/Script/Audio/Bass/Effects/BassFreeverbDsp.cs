#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a Freeverb DSP implemented and attached entirely by the YargAudio native plugin.
    /// The BASS channel passed to <see cref="Create"/> must outlive this handle.
    /// </summary>
    public sealed class BassFreeverbDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const uint ABI_VERSION = 1;
        private const string EFFECT_NAME = "native Freeverb DSP";

        private BassFreeverbDsp() : base(true)
        {
        }

        /// <summary>
        /// Creates and attaches native Freeverb to a BASS stream.
        /// </summary>
        /// <param name="streamHandle">BASS stream receiving effect.</param>
        /// <param name="dryMix">Dry level, clamped to [0, 1].</param>
        /// <param name="wetMix">Wet level, clamped to [0, 3].</param>
        /// <param name="roomSize">Reverb decay control, clamped to [0, 1].</param>
        /// <param name="damp">High-frequency damping, clamped to [0, 1].</param>
        /// <param name="width">Stereo width, clamped to [0, 1].</param>
        /// <param name="priority">DSP priority. Higher values run earlier.</param>
        /// <returns>Attached DSP, or <c>null</c> if creation fails.</returns>
        public static BassFreeverbDsp? Create(int streamHandle, float dryMix, float wetMix,
            float roomSize, float damp, float width = 1, int priority = 0)
        {
            if (streamHandle == 0 || !IsFinite(dryMix) || !IsFinite(wetMix) ||
                !IsFinite(roomSize) || !IsFinite(damp) || !IsFinite(width))
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, dry={2}, wet={3}, room={4}, damp={5}, width={6}, priority={7}.",
                    EFFECT_NAME, streamHandle, dryMix, wetMix, roomSize, damp, width, priority);
                return null;
            }

            try
            {
                uint nativeVersion = Native.GetAbiVersion();
                if (nativeVersion != ABI_VERSION)
                {
                    YargLogger.LogError(
                        $"Cannot attach {EFFECT_NAME}: ABI mismatch managed={ABI_VERSION}, " +
                        $"native={nativeVersion}, channel={streamHandle}, " +
                        $"platform={PlatformDescription}.");
                    return null;
                }

                int result = Native.Attach(unchecked((uint) streamHandle), dryMix, wetMix,
                    roomSize, damp, width, priority, out BassFreeverbDsp dsp,
                    out int bassError);
                if (result == 0 && dsp != null && !dsp.IsInvalid)
                {
                    return dsp;
                }

                // Native initializes output to null on failure. Dispose unexpected handle so
                // partial success cannot leak through this path.
                dsp?.Dispose();
                YargLogger.LogError(
                    $"Failed to attach {EFFECT_NAME}: result={result}, BASS={bassError}, " +
                    $"channel={streamHandle}, dry={dryMix}, wet={wetMix}, room={roomSize}, " +
                    $"damp={damp}, width={width}, priority={priority}, " +
                    $"platform={PlatformDescription}.");
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception,
                    $"Failed to load {EFFECT_NAME} for channel {streamHandle} " +
                    $"on {PlatformDescription}");
                return null;
            }
        }

        /// <summary>
        /// Clears delay and filter state during next native BASS DSP callback.
        /// </summary>
        public void RequestReset()
        {
            if (IsClosed || IsInvalid)
            {
                return;
            }

            try
            {
                Native.Reset(this);
            }
            catch (ObjectDisposedException)
            {
                // Disposal won race with reset request.
            }
        }

        protected override bool ReleaseHandle()
        {
            Native.Destroy(handle);
            return true;
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

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

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(uint channel, float dryMix, float wetMix,
                float roomSize, float damp, float width, int priority,
                out BassFreeverbDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_reset",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Reset(BassFreeverbDsp dsp);

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Destroy(IntPtr dsp);
        }
    }
}
