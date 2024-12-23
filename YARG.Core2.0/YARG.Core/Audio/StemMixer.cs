using System;
using System.Collections.Generic;
using System.IO;

namespace YARG.Core.Audio
{
    public abstract class StemMixer : IDisposable
    {
        private bool _disposed;
        private bool _isPaused = true;

        protected readonly AudioManager _manager;
        protected readonly List<StemChannel> _channels = new();
        protected readonly bool _clampStemVolume;

        protected double _length;
        protected Action? _songEnd;

        public readonly string Name;

        public double Length => _length;
        public IReadOnlyList<StemChannel> Channels => _channels;
        public bool IsPaused => _isPaused;

        public abstract event Action SongEnd;

        protected StemMixer(string name, AudioManager manager,bool clampStemVolume)
        {
            Name = name;
            _manager = manager;
            _clampStemVolume = clampStemVolume;

            _manager.AddMixer(this);
        }

        public StemChannel? this[SongStem stem] => _channels.Find(x => x.Stem == stem);

        public int Play(bool restartBuffer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }

                int ret = Play_Internal(restartBuffer);
                if (ret != 0)
                {
                    return ret;
                }
                _isPaused = false;
                return 0;
            }
        }

        public void FadeIn(double maxVolume, double duration)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeIn_Internal(maxVolume, duration);
                }
            }
        }
        public void FadeOut(double duration)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    FadeOut_Internal(duration);
                }
            }
        }

        public int Pause()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }

                int ret = Pause_Internal();
                if (ret != 0)
                {
                    return ret;
                }
                _isPaused = true;
                return 0;
            }
        }

        public double GetPosition()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return 0;
                }
                return GetPosition_Internal();
            }
        }

        public double GetVolume()
        {
            lock (this)
            {
                if (_disposed)
                {
                    return 0;
                }
                return GetVolume_Internal();
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

        public void SetVolume(double volume)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetVolume_Internal(volume);
                }
            }
        }

        public int GetData(float[] buffer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return -1;
                }
                return GetData_Internal(buffer);
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

        public bool AddChannel(SongStem stem)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem);
            }
        }

        public bool AddChannel(SongStem stem, Stream stream)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem, stream);
            }
        }

        public bool AddChannel(SongStem stem, int[] indices, float[] panning)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return AddChannel_Internal(stem, indices, panning);
            }
        }

        public bool RemoveChannel(SongStem stemToRemove)
        {
            lock (this)
            {
                if (_disposed)
                {
                    return false;
                }
                return RemoveChannel_Internal(stemToRemove);
            }
        }

        internal void ToggleBuffer(bool enable)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    ToggleBuffer_Internal(enable);
                }
            }
        }

        internal void SetBufferLength(int length)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    SetBufferLength_Internal(length);
                }
            }
        }

        protected abstract int Play_Internal(bool restartBuffer);
        protected abstract void FadeIn_Internal(double maxVolume, double duration);
        protected abstract void FadeOut_Internal(double duration);
        protected abstract int Pause_Internal();
        protected abstract double GetPosition_Internal();
        protected abstract double GetVolume_Internal();
        protected abstract void SetPosition_Internal(double position);
        protected abstract void SetVolume_Internal(double volume);
        protected abstract int  GetData_Internal(float[] buffer);
        protected abstract void SetSpeed_Internal(float speed, bool shiftPitch);
        protected abstract bool AddChannel_Internal(SongStem stem);
        protected abstract bool AddChannel_Internal(SongStem stem, Stream stream);
        protected abstract bool AddChannel_Internal(SongStem stem, int[] indices, float[] panning);
        protected abstract bool RemoveChannel_Internal(SongStem stemToRemove);
        protected abstract void ToggleBuffer_Internal(bool enable);
        protected abstract void SetBufferLength_Internal(int length);

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    Pause();
                    _songEnd = null;
                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _manager.RemoveMixer(this);
                    _disposed = true;
                }
            }
        }

        ~StemMixer()
        {
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            Dispose(disposing: true);
        }
    }
}
