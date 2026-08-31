#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>Owns one native scheduled one-shot source and its BASS stream.</summary>
    internal sealed class BassNativeOneShotStream : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const string EFFECT_NAME = "native one-shot stream";

        private readonly object _lifecycleLock = new();
        private int _mixerHandle;
        private float _volume = 1;
        private bool _enabled = true;

        private BassNativeOneShotStream() : base(true)
        {
        }

        internal static BassNativeOneShotStream? Create(int sampleRate, int channels,
            float[] sample, double[] schedule, double leadTime)
        {
            if (!sampleRate.IsValidSampleRate() || !channels.IsValidChannelCount() ||
                !sample.IsValidSampleBuffer(channels) || schedule == null ||
                !leadTime.IsValidLeadTime())
            {
                return null;
            }

            var config = new NativeConfig
            {
                Size = (uint) Marshal.SizeOf<NativeConfig>(),
                SampleRate = checked((uint) sampleRate),
                Channels = checked((uint) channels),
                LeadTime = leadTime
            };

            GCHandle samplePin = default;
            GCHandle schedulePin = default;
            BassNativeOneShotStream? stream = null;
            try
            {
                samplePin = GCHandle.Alloc(sample, GCHandleType.Pinned);
                var samplePointer = samplePin.AddrOfPinnedObject();
                var schedulePointer = IntPtr.Zero;
                if (schedule.Length > 0)
                {
                    schedulePin = GCHandle.Alloc(schedule, GCHandleType.Pinned);
                    schedulePointer = schedulePin.AddrOfPinnedObject();
                }

                int result = YargAudioBindings.OneShotStreamCreate(
                    in config,
                    samplePointer,
                    (ulong) sample.LongLength,
                    schedulePointer,
                    (ulong) schedule.LongLength,
                    out stream,
                    out int bassError);

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
                if (schedulePin.IsAllocated)
                {
                    schedulePin.Free();
                }

                if (samplePin.IsAllocated)
                {
                    samplePin.Free();
                }
            }
        }

        internal bool Attach(int mixerHandle, double anchorSongPosition,
            float playbackSpeed, bool paused)
        {
            lock (_lifecycleLock)
            {
                if (!IsUsable || mixerHandle == 0)
                {
                    return false;
                }

                int result = YargAudioBindings.OneShotStreamAttach(
                    stream: this,
                    mixer: unchecked((uint) mixerHandle),
                    anchorSongPosition: anchorSongPosition,
                    playbackSpeed: playbackSpeed,
                    paused: paused ? 1 : 0,
                    out int bassError);

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
                if (!IsUsable || _mixerHandle == 0)
                {
                    return false;
                }

                try
                {
                    int result = YargAudioBindings.OneShotStreamResync(
                        stream: this,
                        mixer: unchecked((uint) _mixerHandle),
                        anchorSongPosition: anchorSongPosition,
                        playbackSpeed: playbackSpeed,
                        clearActiveVoices: clearActiveVoices ? 1 : 0,
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
                if (!IsUsable || _mixerHandle == 0)
                {
                    return true;
                }

                int result = YargAudioBindings.OneShotStreamDetach(this, out int bassError);
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
                if (!IsUsable)
                {
                    return false;
                }

                int result = YargAudioBindings.OneShotStreamSetPaused(
                    stream: this,
                    mixer: unchecked((uint) _mixerHandle),
                    paused: paused ? 1 : 0,
                    out int bassError);

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
                if (!volume.IsFinite())
                {
                    return false;
                }

                var value = (float) volume;
                if (!value.IsFinite())
                {
                    return false;
                }

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
                return YargAudioBindings.OneShotStreamDestroy(handle, out _) == 0;
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
            if (!IsUsable)
            {
                return false;
            }

            float gain = _enabled ? volume : 0;
            int result = YargAudioBindings.OneShotStreamSetGain(this, gain);
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
        internal struct NativeConfig
        {
            internal uint Size;
            internal uint SampleRate;
            internal uint Channels;
            internal uint Reserved;
            internal double LeadTime;
        }
    }

    internal static class BassNativeOneShotStreamExtensions
    {
        internal static bool IsValidSampleRate(this int sampleRate) => sampleRate > 0;

        internal static bool IsValidChannelCount(this int channels) => channels > 0;

        internal static bool IsValidSampleBuffer(this float[]? sample, int channels) =>
            sample != null && sample.Length > 0 && sample.Length % channels == 0;

        internal static bool IsValidLeadTime(this double leadTime) =>
            !double.IsNaN(leadTime) && !double.IsInfinity(leadTime) && leadTime >= 0;

        internal static bool IsFinite(this double value) =>
            !double.IsNaN(value) && !double.IsInfinity(value);

        internal static bool IsFinite(this float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
