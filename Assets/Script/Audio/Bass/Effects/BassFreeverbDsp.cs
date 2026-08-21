#nullable enable
using System.Runtime.InteropServices;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    /// <summary>
    /// Owns a Freeverb DSP implemented and attached entirely by the YargAudio native plugin.
    /// The BASS channel passed to <see cref="Create"/> must outlive this handle.
    /// </summary>
    public sealed class BassFreeverbDsp : NativeReverbDspHandle<BassFreeverbDsp.FreeverbParams>
    {
        private const string EFFECT_NAME = "native Freeverb DSP";

        private BassFreeverbDsp() : base()
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
            if (streamHandle == 0 || !YargAudioNative.AreFinite(dryMix, wetMix, roomSize, damp, width))
            {
                YargLogger.LogFormatError(
                    "Cannot attach {0}: channel={1}, dry={2}, wet={3}, room={4}, damp={5}, width={6}, priority={7}.",
                    EFFECT_NAME, streamHandle, dryMix, wetMix, roomSize, damp, width, priority);
                return null;
            }

            return YargAudioNative.Attach(EFFECT_NAME, streamHandle,
                (out BassFreeverbDsp handle, out int bassError) =>
                    Native.Attach(unchecked((uint) streamHandle), dryMix, wetMix,
                        roomSize, damp, width, priority, out handle, out bassError));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FreeverbParams
        {
            public uint Size;
            public float DryMix;
            public float WetMix;
            public float RoomSize;
            public float Damp;
            public float Width;

            public FreeverbParams(float dryMix, float wetMix, float roomSize, float damp, float width)
            {
                Size = (uint) Marshal.SizeOf<FreeverbParams>();
                DryMix = dryMix;
                WetMix = wetMix;
                RoomSize = roomSize;
                Damp = damp;
                Width = width;
            }
        }

        protected override void Destroy(System.IntPtr handle)
        {
            Native.Destroy(handle);
        }

        protected override int NativeReset()
        {
            return Native.Reset(this);
        }

        protected override int NativeSetParams(in FreeverbParams parms)
        {
            return Native.SetParams(this, in parms);
        }

        protected override FreeverbParams CreateParams(float dryMix, float wetMix, float roomSize, float damp, float width)
        {
            return new FreeverbParams(dryMix, wetMix, roomSize, damp, width);
        }

        protected override bool AreFinite(in FreeverbParams parms)
        {
            if (!YargAudioNative.AreFinite(parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width))
            {
                YargLogger.LogFormatError("Ignoring non-finite Freeverb params for {0}: dry={1}, wet={2}, room={3}, damp={4}, width={5}.", EFFECT_NAME, parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width);
                return false;
            }

            return true;
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(uint channel, float dryMix, float wetMix,
                float roomSize, float damp, float width, int priority,
                out BassFreeverbDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_reset",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Reset(BassFreeverbDsp dsp);

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_set_params",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetParams(BassFreeverbDsp dsp, in FreeverbParams parms);

            [DllImport(LIBRARY, EntryPoint = "yarg_freeverb_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Destroy(System.IntPtr dsp);
        }
    }
}
