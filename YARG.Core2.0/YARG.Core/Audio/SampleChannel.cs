using System;
using System.Collections.Generic;
using System.Text;

namespace YARG.Core.Audio
{
    public abstract class SampleChannel : IDisposable
    {
        protected const double PLAYBACK_SUPPRESS_THRESHOLD = 0.05f;
        private bool _disposed;

        protected readonly string _path;
        protected readonly int _playbackCount;

        public readonly SfxSample Sample;
        protected SampleChannel(SfxSample sample, string path, int playbackCount)
        {
            Sample = sample;
            _path = path;
            _playbackCount = playbackCount;

            GlobalAudioHandler.StemSettings[SongStem.Sfx].OnVolumeChange += SetVolume;
        }

        public void Play()
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Play_Internal();
                }
            }
        }

        private void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(volume);
                }
            }
        }

        protected abstract void Play_Internal();
        protected abstract void SetVolume_Internal(double volume);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    GlobalAudioHandler.StemSettings[SongStem.Sfx].OnVolumeChange -= SetVolume;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~SampleChannel()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
