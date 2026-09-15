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
                    YargAudioBindings.DattorroReverbDspAttach(unchecked((uint) streamHandle), dryMix, wetMix,
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

        protected override void Destroy(System.IntPtr handle) =>
            YargAudioBindings.DattorroReverbDspDestroy(handle);

        protected override int NativeReset() =>
            YargAudioBindings.DattorroReverbDspReset(this);

        protected override int NativeSetParams(in DattorroReverbParams parms) =>
            YargAudioBindings.DattorroReverbDspSetParams(this, in parms);

        protected override DattorroReverbParams CreateParams(float dryMix, float wetMix, float roomSize, float damp, float width) =>
            new DattorroReverbParams(dryMix, wetMix, roomSize, damp, width);

        protected override bool AreFinite(in DattorroReverbParams parms)
        {
            if (!YargAudioNative.AreFinite(parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width))
            {
                YargLogger.LogFormatError("Ignoring non-finite Dattorro params for {0}: dry={1}, wet={2}, room={3}, damp={4}, width={5}.", EFFECT_NAME, parms.DryMix, parms.WetMix, parms.RoomSize, parms.Damp, parms.Width);
                return false;
            }

            return true;
        }
    }
}
