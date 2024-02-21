using System;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;

namespace YARG.Audio
{
    public interface IStemChannel<TAudioManager> : IDisposable
        where TAudioManager : IAudioManager
    {
        public SongStem Stem { get; }
        public double LengthD { get; }
        public float LengthF => (float) LengthD;

        public double Volume { get; }

        public event Action ChannelEnd;

        public void FadeIn(float maxVolume);
        public UniTask FadeOut();

        public void SetVolume(TAudioManager manager, double newVolume);

        public void SetReverb(TAudioManager manager, bool reverb);

        public void SetSpeed(float speed);
        public void SetWhammyPitch(TAudioManager manager, float percent);

        public double GetPosition(TAudioManager manager, bool bufferCompensation = true);
        public void SetPosition(TAudioManager manager, double position, bool bufferCompensation = true);

        public double GetLengthInSeconds();
    }
}