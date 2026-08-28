#nullable enable
using System.Runtime.InteropServices;
using YARG.Audio.BASS.Native;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Effects
{
    public sealed class BassDattorroReverbDsp : NativeReverbDspHandle<BassDattorroReverbDsp.DattorroReverbParams>
    {
        private const string EFFECT_NAME = "native Dattorro reverb DSP";

        private BassDattorroReverbDsp() : base()
        {
        }

        public static BassDattorroReverbDsp? Create(int streamHandle, float dryMix, float wetMix,
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
                (out BassDattorroReverbDsp handle, out int bassError) =>
                    Native.Attach(unchecked((uint) streamHandle), dryMix, wetMix,
                        roomSize, damp, width, priority, out handle, out bassError));
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DattorroReverbParams
        {
            public uint Size;
            public float DryMix;
            public float WetMix;
            public float RoomSize;
            public float Damp;
            public float Width;

            public DattorroReverbParams(float dryMix, float wetMix, float roomSize, float damp, float width)
            {
                Size = (uint) Marshal.SizeOf<DattorroReverbParams>();
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

        protected override int NativeSetParams(in DattorroReverbParams parms)
        {
            return Native.SetParams(this, in parms);
        }

        protected override DattorroReverbParams CreateParams(float dryMix, float wetMix, float roomSize, float damp, float width)
        {
            return new DattorroReverbParams(dryMix, wetMix, roomSize, damp, width);
        }

        protected override bool AreFinite(in DattorroReverbParams parms)
        {
            if (!YargAudioNative.AreFinite(parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width))
            {
                YargLogger.LogFormatError("Ignoring non-finite Dattorro params for {0}: dry={1}, wet={2}, room={3}, damp={4}, width={5}.", EFFECT_NAME, parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width);
                return false;
            }

            return true;
        }

        private static class Native
        {
            private const string LIBRARY = "yarg_audio";

            [DllImport(LIBRARY, EntryPoint = "yarg_dattorro_reverb_dsp_attach",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Attach(uint channel, float dryMix, float wetMix,
                float roomSize, float damp, float width, int priority,
                out BassDattorroReverbDsp dsp, out int bassError);

            [DllImport(LIBRARY, EntryPoint = "yarg_dattorro_reverb_dsp_reset",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int Reset(BassDattorroReverbDsp dsp);

            [DllImport(LIBRARY, EntryPoint = "yarg_dattorro_reverb_dsp_set_params",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int SetParams(BassDattorroReverbDsp dsp, in DattorroReverbParams parms);

            [DllImport(LIBRARY, EntryPoint = "yarg_dattorro_reverb_dsp_destroy",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern void Destroy(System.IntPtr dsp);
        }
    }
}
