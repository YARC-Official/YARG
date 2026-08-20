using System;

namespace YARG.Audio.BASS.Effects
{
    public interface IBassReverbDsp : IDisposable
    {
        void RequestReset();

        bool SetParams(float dryMix, float wetMix, float roomSize, float damp, float width);
    }
}
