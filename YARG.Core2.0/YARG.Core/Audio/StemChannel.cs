using System;

namespace YARG.Core.Audio
{
    public abstract class StemChannel : IDisposable
    {
        public const double MINIMUM_STEM_VOLUME = 0.15;

        private bool _disposed;

        private readonly bool _clampVolume;
        protected readonly AudioManager _manager;
        public readonly SongStem Stem;

        protected StemChannel(AudioManager manager, SongStem stem, bool clampVolume)
        {
            _clampVolume = clampVolume;
            _manager = manager;
            Stem = stem;

            var settings = GlobalAudioHandler.StemSettings[Stem];
            settings.OnVolumeChange += SetVolume;
            settings.OnReverbChange += SetReverb;
            settings.OnWhammyPitchChange += SetWhammyPitch;
        }

        public void SetWhammyPitch(float percent)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetWhammyPitch_Internal(percent);
                }
            }
        }

        public void SetPosition(double position)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetPosition_Internal(position);
                }
            }
        }

        public void SetSpeed(float speed, bool shiftPitch)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetSpeed_Internal(speed, shiftPitch);
                }
            }
        }

        private void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    if (_clampVolume && volume < MINIMUM_STEM_VOLUME)
                    {
                        volume = MINIMUM_STEM_VOLUME;
                    }
                    SetVolume_Internal(volume);
                }
            }
        }

        private void SetReverb(bool reverb)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetReverb_Internal(reverb);
                }
            }
        }

        protected abstract void SetWhammyPitch_Internal(float percent);
        protected abstract void SetPosition_Internal(double position);
        protected abstract void SetSpeed_Internal(float speed, bool shiftPitch);

        protected abstract void SetVolume_Internal(double newVolume);
        protected abstract void SetReverb_Internal(bool reverb);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    GlobalAudioHandler.StemSettings[Stem].OnVolumeChange -= SetVolume;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~StemChannel()
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
