﻿using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using YARG.Audio.BASS;
using YARG.Core.Audio;

namespace YARG.Audio
{
    public interface IStemMixer<TAudioManager, TChannel> : IDisposable
        where TAudioManager : IAudioManager
        where TChannel : IStemChannel<TAudioManager>
    {
        public bool IsPlaying { get; }

        public event Action SongEnd;

        public IReadOnlyList<TChannel> Channels { get; }

        public TChannel LeadChannel { get; }

        public int Play(bool restart = false);

        public void FadeIn(float maxVolume);
        public UniTask FadeOut(CancellationToken token = default);

        public int Pause();

        public double GetPosition(TAudioManager manager, bool bufferCompensation = true);

        public void SetPosition(TAudioManager manager, double position, bool bufferCompensation = true);

        public int GetData(float[] buffer);

        public void SetPlayVolume(TAudioManager manager, bool fadeIn);

        public void SetSpeed(float speed);

#nullable enable
        public int AddChannel(BassStemChannel channel, int[]? indices, float[]? panning);
#nullable disable

        public bool RemoveChannel(SongStem stemToRemove);

#nullable enable
        public TChannel? GetChannel(SongStem stem);
#nullable disable
    }
}