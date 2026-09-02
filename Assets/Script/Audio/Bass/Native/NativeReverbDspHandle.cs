#nullable enable
using YARG.Audio.BASS.Effects;

namespace YARG.Audio.BASS.Native
{
    public abstract class NativeReverbDspHandle<TParams> : NativeDspHandle, IBassReverbDsp where TParams : unmanaged
    {
        protected abstract int NativeReset();

        protected abstract int NativeSetParams(in TParams parms);

        protected abstract TParams CreateParams(float dryMix, float wetMix, float roomSize, float damp, float width);

        protected abstract bool AreFinite(in TParams parms);

        public void RequestReset()
        {
            YargAudioNative.TryReset(this, _ => NativeReset());
        }

        public bool SetParams(float dryMix, float wetMix, float roomSize, float damp, float width)
        {
            return SetParams(CreateParams(dryMix, wetMix, roomSize, damp, width));
        }

        public bool SetParams(in TParams parms)
        {
            if (!AreFinite(parms))
            {
                return false;
            }

            var parameters = parms;
            return YargAudioNative.TryInvoke(this, _ => NativeSetParams(parameters));
        }
    }
}
