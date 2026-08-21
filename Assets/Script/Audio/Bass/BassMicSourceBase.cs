#nullable enable
using System;
using YARG.Core.Audio;

namespace YARG.Audio.BASS
{
    internal abstract class BassMicSourceBase : IBassMicSource
    {
        private readonly object _lock = new();
        private bool _disposed;

        protected object SyncRoot => _lock;

        protected bool IsDisposed
        {
            get
            {
                lock (_lock)
                {
                    return _disposed;
                }
            }
        }

        protected BassMicSourceBase(string baseName, string displayName, int channel)
        {
            BaseName = baseName;
            DisplayName = displayName;
            Channel = channel;
        }

        public string DisplayName { get; }
        public string BaseName { get; }
        public int Channel { get; }

        public event Action? InputChanged;

        public int SampleRate
        {
            get
            {
                lock (_lock)
                {
                    return GetSampleRateCore();
                }
            }
        }

        public bool IsValid
        {
            get
            {
                lock (_lock)
                {
                    if (_disposed)
                    {
                        return false;
                    }

                    return GetIsValidCore();
                }
            }
        }

        public int Read(Span<float> destination)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return -1;
                }

                return ReadCore(destination);
            }
        }

        public int GetBacklogBytes()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return -1;
                }

                return GetBacklogBytesCore();
            }
        }

        public bool TryCreateRecordingChannel(bool withEffects, out int handle, out int sampleRate)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    handle = 0;
                    sampleRate = 0;
                    return false;
                }

                sampleRate = GetSampleRateCore();
                return TryCreateRecordingChannelCore(withEffects, out handle);
            }
        }

        public void ReleaseRecordingChannel(int handle)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                ReleaseRecordingChannelCore(handle);
            }
        }

        public bool ResetToLive()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return false;
                }

                return ResetToLiveCore();
            }
        }

        public bool SetMonitoringLevel(float volume)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return false;
                }

                return SetMonitoringLevelCore(volume);
            }
        }

        public bool SetReverbLevel(float wet)
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return false;
                }

                return SetReverbLevelCore(wet);
            }
        }

        public abstract bool Reset();

        public MicBufferInfo? GetBufferInfo()
        {
            lock (_lock)
            {
                if (_disposed)
                {
                    return null;
                }

                return GetBufferInfoCore();
            }
        }

        public void Dispose()
        {
            bool shouldDispose = false;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                shouldDispose = true;
            }

            if (shouldDispose)
            {
                DisposeCore();
            }
        }

        protected void RaiseInputChanged()
        {
            Action? handler;
            lock (_lock)
            {
                if (_disposed)
                {
                    return;
                }

                handler = InputChanged;
            }

            handler?.Invoke();
        }

        protected abstract int GetSampleRateCore();
        protected abstract bool GetIsValidCore();
        protected abstract int ReadCore(Span<float> destination);
        protected abstract int GetBacklogBytesCore();
        protected abstract bool TryCreateRecordingChannelCore(bool withEffects, out int handle);
        protected abstract void ReleaseRecordingChannelCore(int handle);
        protected abstract bool ResetToLiveCore();
        protected abstract bool SetMonitoringLevelCore(float volume);
        protected abstract bool SetReverbLevelCore(float wet);
        protected abstract MicBufferInfo? GetBufferInfoCore();
        protected abstract void DisposeCore();
    }
}
