using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;

namespace YARG.Audio
{
    public interface IStemMixer<TAudioManager, TChannel> : IDisposable
        where TAudioManager : IAudioManager
        where TChannel : IStemChannel<TAudioManager>
    {
        public int StemsLoaded { get; }

        public bool IsPlaying { get; }

        public event Action SongEnd;

        public IReadOnlyList<TChannel> Channels { get; }

        public TChannel LeadChannel { get; }

        public bool Create();

        public int Play(bool restart = false);

        public void FadeIn(float maxVolume);
        public UniTask FadeOut(CancellationToken token = default);

        public int Pause();

        public double GetPosition(TAudioManager manager, bool bufferCompensation = true);

        public void SetPosition(TAudioManager manager, double position, bool bufferCompensation = true);

        public int GetData(float[] buffer);

        public void SetPlayVolume(TAudioManager manager, bool fadeIn);

        public void SetSpeed(float speed);

        public int AddChannel(TChannel channel);

        public int AddChannel(TChannel channel, int[] indices, float[] panning);

        public bool RemoveChannel(SongStem stemToRemove);

        public TChannel[] GetChannels(SongStem stem);
    }
}