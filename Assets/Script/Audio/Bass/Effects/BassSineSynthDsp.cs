#nullable enable
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a synthesized tone DSP implemented and rendered entirely by the YargAudio native
    /// plugin. The schedule, oscillator, and segment scan stay native, so no managed code runs on
    /// the audio render thread.
    /// </summary>
    internal sealed class BassSineSynthDsp : SafeHandleZeroOrMinusOneIsInvalid
    {
        private const string EFFECT_NAME = "native tone DSP";

        private readonly object _lifecycleLock = new();
        private          int    _channelHandle;

        private BassSineSynthDsp() : base(true)
        {
        }

        /// <summary>Whether the DSP is attached to a mixer. False after <see cref="Detach"/>.</summary>
        internal bool IsAttached
        {
            get
            {
                lock (_lifecycleLock)
                {
                    return _channelHandle != 0;
                }
            }
        }

        /// <param name="tempoStreamHandle">Channel whose decode position yields the song position.</param>
        /// <param name="volume">Tone volume relative to the mix.</param>
        /// <param name="fadeSeconds">Seconds for a full volume ramp, used to declick note edges.</param>
        internal static BassSineSynthDsp? Create(int tempoStreamHandle, float volume, float fadeSeconds)
        {
            if (tempoStreamHandle == 0 || !IsFinite(volume) || !IsFinite(fadeSeconds) ||
                volume < 0 || fadeSeconds <= 0)
            {
                YargLogger.LogFormatError(
                    "Cannot create {0}: tempoStream={1}, volume={2}, fadeSeconds={3}.",
                    EFFECT_NAME, tempoStreamHandle, volume, fadeSeconds);
                return null;
            }

            var config = new NativeConfig
            {
                Size = (uint) Marshal.SizeOf<NativeConfig>(),
                TempoStream = unchecked((uint) tempoStreamHandle),
                Volume = volume,
                FadeSeconds = fadeSeconds,
                // Broadcast to every channel until the song routing publishes the configured
                // output channel; SetOutputChannel is called before the DSP is attached.
                OutputChannel = 0,
            };

            try
            {
                uint nativeVersion = Native.GetAbiVersion();
                if (nativeVersion != BassHelpers.YARG_AUDIO_ABI_VERSION)
                {
                    YargLogger.LogFormatError(
                        "Cannot create {0}: ABI mismatch managed={1}, native={2}.",
                        EFFECT_NAME, BassHelpers.YARG_AUDIO_ABI_VERSION, nativeVersion);
                    return null;
                }

                int result = Native.Create(ref config, out var dsp);
                if (result == 0 && dsp != null && !dsp.IsInvalid)
                {
                    return dsp;
                }

                dsp?.Dispose();
                YargLogger.LogFormatError("Failed to create {0}: result={1}.", EFFECT_NAME, result);
                return null;
            }
            catch (Exception exception) when (exception is DllNotFoundException or
                EntryPointNotFoundException or BadImageFormatException)
            {
                YargLogger.LogException(exception, $"Failed to load {EFFECT_NAME}");
                return null;
            }
        }

        internal bool Attach(int channelHandle, int priority = 0)
        {
            lock (_lifecycleLock)
            {
                if (IsClosed || IsInvalid || channelHandle == 0)
                {
                    return false;
                }

                if (_channelHandle != 0)
                {
                    Detach_NoLock();
                }

                int result = Native.Attach(this, unchecked((uint) channelHandle), priority,
                    out int bassError);
                if (result != 0)
                {
                    YargLogger.LogFormatError("Failed to attach {0} to mixer {1}: result={2}, BASS={3}.",
                        EFFECT_NAME, channelHandle, result, bassError);
                    return false;
                }

                _channelHandle = channelHandle;
                return true;
            }
        }

        internal void Detach()
        {
            lock (_lifecycleLock)
            {
                Detach_NoLock();
            }
        }

        private void Detach_NoLock()
        {
            if (_channelHandle == 0 || IsClosed || IsInvalid)
            {
                _channelHandle = 0;
                return;
            }

            // Native tolerates the channel already being freed, which is the teardown case.
            // Anything else means the DSP is still installed on a live mixer, so keep the
            // handle: clearing it would let Reattach install a second DSP on the same mixer.
            int result = Native.Detach(this, out int bassError);
            if (result != 0)
            {
                YargLogger.LogFormatError("Failed to detach {0} from mixer {1}: result={2}, BASS={3}.",
                    EFFECT_NAME, _channelHandle, result, bassError);
                return;
            }

            _channelHandle = 0;
        }

        /// <summary>
        /// Replaces the pitch schedule. Native copies the segments, so the span need not outlive the
        /// call, and swaps them under the channel lock so the render thread never sees a partial table.
        /// </summary>
        internal unsafe bool SetSchedule(ReadOnlySpan<ToneSegment> segments)
        {
            if (IsClosed || IsInvalid)
            {
                return false;
            }

            // Copied into the native layout rather than pinning the Core type directly. ToneSegment
            // is backend-agnostic and has to stay free to change shape; only this struct is pinned
            // to the plugin ABI, the same way NativeConfig is.
            var native = new NativeSegment[segments.Length];
            for (int i = 0; i < segments.Length; i++)
            {
                native[i] = new NativeSegment(segments[i]);
            }

            fixed (NativeSegment* pointer = native)
            {
                int result = Native.SetSchedule(this, (IntPtr) pointer, (ulong) segments.Length,
                    out int bassError);
                if (result == 0)
                {
                    return true;
                }

                YargLogger.LogFormatError("Failed to set {0} schedule: result={1}, BASS={2}.",
                    EFFECT_NAME, result, bassError);
                return false;
            }
        }

        /// <summary>
        /// Publishes the mapping from tempo stream seconds to song position, and the playback speed
        /// used to scale a block's song duration.
        /// </summary>
        internal bool SetTiming(double songTimeOffset, float playbackSpeed)
        {
            if (IsClosed || IsInvalid)
            {
                return false;
            }

            return Native.SetTiming(this, songTimeOffset, playbackSpeed) == 0;
        }

        /// <summary>
        /// Publishes the output channel the tone renders into: a 1-based value matching the
        /// experimental output-channel setting (the odd channel of a speaker pair), or 0 to
        /// broadcast to every channel. A value past the device's channel count falls back to
        /// broadcasting on the render thread rather than dropping the tone.
        /// </summary>
        internal bool SetOutputChannel(uint outputChannel)
        {
            if (IsClosed || IsInvalid)
            {
                return false;
            }

            return Native.SetOutputChannel(this, outputChannel) == 0;
        }

        protected override bool ReleaseHandle()
        {
            return Native.Destroy(handle) == 0;
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// Mirrors yarg_tone_segment. Field order and types are fixed by the plugin ABI.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSegment
        {
            public double StartTime;
            public double EndTime;
            public float  StartPitch;
            public float  EndPitch;

            public NativeSegment(in ToneSegment segment)
            {
                StartTime = segment.StartTime;
                EndTime = segment.EndTime;
                StartPitch = segment.StartPitch;
                EndPitch = segment.EndPitch;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeConfig
        {
            public uint  Size;
            public uint  TempoStream;
            public float Volume;
            public float FadeSeconds;
            public uint  OutputChannel;
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_audio_get_abi_version",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern uint GetAbiVersion();

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_create",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Create(ref NativeConfig config, out BassSineSynthDsp dsp);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(BassSineSynthDsp dsp, uint channel, int priority,
                out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_detach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Detach(BassSineSynthDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_set_schedule",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetSchedule(BassSineSynthDsp dsp, IntPtr segments, ulong segmentCount,
                out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_set_timing",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetTiming(BassSineSynthDsp dsp, double songTimeOffset,
                float playbackSpeed);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_set_output_channel",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetOutputChannel(BassSineSynthDsp dsp, uint outputChannel);

            [DllImport(LIBRARY, EntryPoint = "yarg_sine_synth_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Destroy(IntPtr dsp);
        }
    }
}
