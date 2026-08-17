#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>Owns one native scheduled one-shot source and its BASS stream.</summary>
    internal sealed class BassNativeOneShotStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const string EFFECT_NAME = "native one-shot stream";

        private readonly object _lifecycleLock = new object();
        private int _mixerHandle;
        private float _volume = 1;
        private bool _enabled = true;

        private BassNativeOneShotStream() : base(true)
        {
        }

        internal static BassNativeOneShotStream? Create(int sampleRate, int channels,
            float[] sample, double[] schedule, double leadTime)
        {
            if (sampleRate <= 0 || channels <= 0 || sample == null || schedule == null ||
                sample.Length == 0 ||
                sample.Length % channels != 0 || double.IsNaN(leadTime) ||
                double.IsInfinity(leadTime) || leadTime < 0)
            {
                return null;
            }

            var config = new NativeConfig
            {
                Size = (uint) Marshal.SizeOf<NativeConfig>(),
                SampleRate = (uint) sampleRate,
                Channels = (uint) channels,
                LeadTime = leadTime
            };

            GCHandle samplePin = default;
            GCHandle schedulePin = default;
            BassNativeOneShotStream? stream = null;
            try
            {
                samplePin = GCHandle.Alloc(sample, GCHandleType.Pinned);
                IntPtr samplePointer = samplePin.AddrOfPinnedObject();
                IntPtr schedulePointer = IntPtr.Zero;
                if (schedule.Length > 0)
                {
                    schedulePin = GCHandle.Alloc(schedule, GCHandleType.Pinned);
                    schedulePointer = schedulePin.AddrOfPinnedObject();
                }

                int result = Native.Create(ref config, samplePointer, (ulong) sample.LongLength,
                    schedulePointer, (ulong) schedule.LongLength, out stream, out int bassError);
                if (result == 0 && stream != null && !stream.IsInvalid)
                {
                    return stream;
                }

                stream?.Dispose();
                LogFailure("create", result, bassError);
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception, $"Failed to load {EFFECT_NAME}");
                return null;
            }
            finally
            {
                if (schedulePin.IsAllocated) schedulePin.Free();
                if (samplePin.IsAllocated) samplePin.Free();
            }
        }

        internal bool Attach(int mixerHandle, double anchorSongPosition,
            float playbackSpeed, bool paused)
        {
            lock (_lifecycleLock)
            {
                if (!IsUsable || mixerHandle == 0) return false;
                int result = Native.Attach(this, unchecked((uint) mixerHandle),
                    anchorSongPosition, playbackSpeed, paused ? 1 : 0, out int bassError);
                if (result != 0)
                {
                    LogFailure("attach", result, bassError);
                    return false;
                }
                _mixerHandle = mixerHandle;
                return true;
            }
        }

        internal bool Resync(double anchorSongPosition, float playbackSpeed,
            bool clearActiveVoices)
        {
            lock (_lifecycleLock)
            {
                if (!IsUsable || _mixerHandle == 0) return false;
                try
                {
                    int result = Native.Resync(this, unchecked((uint) _mixerHandle),
                        anchorSongPosition, playbackSpeed, clearActiveVoices ? 1 : 0,
                        out int bassError);
                    if (result != 0)
                    {
                        LogFailure("resync", result, bassError);
                        return false;
                    }
                    return true;
                }
                catch (Exception exception) when (exception is DllNotFoundException or
                    EntryPointNotFoundException or BadImageFormatException)
                {
                    YargLogger.LogException(exception, $"Failed to resync {EFFECT_NAME}");
                    return false;
                }
            }
        }

        internal bool Detach()
        {
            lock (_lifecycleLock)
            {
                if (!IsUsable || _mixerHandle == 0) return true;
                int result = Native.Detach(this, out int bassError);
                if (result != 0)
                {
                    LogFailure("detach", result, bassError);
                    return false;
                }
                _mixerHandle = 0;
                return true;
            }
        }

        internal bool SetPaused(bool paused)
        {
            lock (_lifecycleLock)
            {
                if (!IsUsable) return false;
                int result = Native.SetPaused(this, unchecked((uint) _mixerHandle),
                    paused ? 1 : 0, out int bassError);
                if (result != 0)
                {
                    LogFailure("set pause", result, bassError);
                    return false;
                }
                return true;
            }
        }

        internal bool SetVolume(double volume)
        {
            lock (_lifecycleLock)
            {
                if (double.IsNaN(volume) || double.IsInfinity(volume)) return false;
                float value = (float) volume;
                if (float.IsNaN(value) || float.IsInfinity(value)) return false;
                return SetEffectiveGainLocked(value);
            }
        }

        internal bool SetEnabled(bool enabled)
        {
            lock (_lifecycleLock)
            {
                _enabled = enabled;
                return IsUsable && SetEffectiveGainLocked(_volume);
            }
        }

        protected override bool ReleaseHandle()
        {
            try
            {
                return Native.Destroy(handle, out _ ) == 0;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                return false;
            }
        }

        private bool IsUsable => !IsClosed && !IsInvalid;

        private bool SetEffectiveGainLocked(float volume)
        {
            if (!IsUsable) return false;
            float gain = _enabled ? volume : 0;
            int result = Native.SetGain(this, gain);
            if (result != 0)
            {
                LogFailure("set gain", result, 0);
                return false;
            }
            _volume = volume;
            return true;
        }

        private static void LogFailure(string operation, int result, int bassError)
        {
            YargLogger.LogError(
                $"Failed to {operation} {EFFECT_NAME}: result={result}, BASS={bassError}.");
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeConfig
        {
            internal uint Size;
            internal uint SampleRate;
            internal uint Channels;
            internal uint Reserved;
            internal double LeadTime;
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_create",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Create(ref NativeConfig config, IntPtr pcm,
                ulong pcmSampleCount, IntPtr schedule, ulong scheduleCount,
                out BassNativeOneShotStream stream, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(BassNativeOneShotStream stream, uint mixer,
                double anchorSongPosition, float playbackSpeed, int paused,
                out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_resync_ex",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Resync(BassNativeOneShotStream stream, uint mixer,
                double anchorSongPosition, float playbackSpeed, int clearActiveVoices,
                out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_set_paused",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetPaused(BassNativeOneShotStream stream, uint mixer,
                int paused, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_set_gain",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetGain(BassNativeOneShotStream stream, float gain);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_detach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Detach(BassNativeOneShotStream stream,
                out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_one_shot_stream_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Destroy(IntPtr stream, out int bassError);
        }
    }
}
