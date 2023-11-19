﻿using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    public class BassMoggStemChannel : BassStemChannel
    {
        public readonly float left;
        public readonly float right;

        public BassMoggStemChannel(IAudioManager manager, SongStem stem, int splitStreams, float left, float right)
            : base(manager, stem, splitStreams, true)
        {
            this.left = left;
            this.right = right;
        }
    }
}