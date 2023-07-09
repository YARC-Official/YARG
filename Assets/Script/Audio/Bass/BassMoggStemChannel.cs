using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace YARG.Audio.BASS
{
    public class BassMoggStemChannel : BassStemChannel
    {
        public readonly float left;
        public readonly float right;

        public BassMoggStemChannel(IAudioManager manager, SongStem stem, int splitStreams, float left, float right) : base(manager, stem, splitStreams)
        {
            this.left = left;
            this.right = right;
        }
    }
}