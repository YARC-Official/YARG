#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Core.Logging;
using YARG.Audio.BASS;

namespace YARG.Audio.BASS.Native
{
    internal delegate int AttachFunc<T>(out T handle, out int bassError) where T : SafeHandle;

    internal static class YargAudioNative
    {
        private const string LIBRARY = "yarg_audio";

        [DllImport(LIBRARY, EntryPoint = "yarg_audio_get_abi_version", CallingConvention = CallingConvention.Cdecl)]
        private static extern uint GetAbiVersion();

        internal static string PlatformDescription =>
            $"{RuntimeInformation.OSDescription}/{RuntimeInformation.ProcessArchitecture}/{IntPtr.Size * 8}-bit";

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        internal static bool AreFinite(params float[] values)
        {
            foreach (var v in values)
            {
                if (!IsFinite(v))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool CheckAbi(string effectName, int channelHandle)
        {
            uint nativeVersion = GetAbiVersion();
            if (nativeVersion != BassHelpers.YARG_AUDIO_ABI_VERSION)
            {
                YargLogger.LogError(
                    $"Cannot attach {effectName}: ABI mismatch managed={BassHelpers.YARG_AUDIO_ABI_VERSION}, " +
                    $"native={nativeVersion}, channel={channelHandle}, " +
                    $"platform={PlatformDescription}.");
                return false;
            }

            return true;
        }

        internal static bool CheckAbi()
        {
            uint nativeVersion = GetAbiVersion();
            if (nativeVersion != BassHelpers.YARG_AUDIO_ABI_VERSION)
            {
                YargLogger.LogFormatError("YargAudio ABI mismatch: managed={0}, native={1}",
                    BassHelpers.YARG_AUDIO_ABI_VERSION, nativeVersion);
                return false;
            }

            return true;
        }

        internal static T? Attach<T>(string effectName, int channelHandle, AttachFunc<T> attach) where T : SafeHandle
        {
            try
            {
                if (!CheckAbi(effectName, channelHandle))
                {
                    return null;
                }

                int result = attach(out T handle, out int bassError);
                if (result == 0 && handle != null && !handle.IsInvalid)
                {
                    return handle;
                }

                handle?.Dispose();
                YargLogger.LogError(
                    $"Failed to attach {effectName}: result={result}, BASS={bassError}, " +
                    $"channel={channelHandle}, platform={PlatformDescription}.");
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception,
                    $"Failed to load {effectName} for channel {channelHandle} " +
                    $"on {PlatformDescription}");
                return null;
            }
        }

        internal static bool TryInvoke<T>(T handle, Func<T, int> call) where T : SafeHandle
        {
            if (handle.IsClosed || handle.IsInvalid)
            {
                return false;
            }

            try
            {
                return call(handle) == 0;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        internal static void TryReset<T>(T handle, Func<T, int> call) where T : SafeHandle
        {
            if (handle.IsClosed || handle.IsInvalid)
            {
                return;
            }

            try
            {
                call(handle);
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
