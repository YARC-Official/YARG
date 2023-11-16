using System;
using YARG.Core.Audio;

namespace YARG.Audio
{
    public interface ISampleChannel : IDisposable
    {
        public SfxSample Sample { get; }

        public int Load();

        public void Play();
    }
}