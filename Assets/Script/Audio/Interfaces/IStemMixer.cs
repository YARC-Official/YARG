﻿using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Core.Audio;

namespace YARG.Audio
{
    public interface IStemMixer : IDisposable
    {
        public int StemsLoaded { get; }

        public bool IsPlaying { get; }

        public event Action SongEnd;

        public IReadOnlyDictionary<SongStem, List<IStemChannel>> Channels { get; }

        public IStemChannel LeadChannel { get; }

        public bool Create();

        public int Play(bool restart = false);

        public void FadeIn(float maxVolume);
        public UniTask FadeOut(CancellationToken token = default);

        public int Pause();

        public double GetPosition(bool bufferCompensation = true);

        public void SetPosition(double position, bool bufferCompensation = true);

        public int GetData(float[] buffer);

        public void SetPlayVolume(bool fadeIn);

        public void SetSpeed(float speed);

        public int AddChannel(IStemChannel channel);

        public bool RemoveChannel(IStemChannel channel);

        public IStemChannel[] GetChannels(SongStem stem);
    }
}