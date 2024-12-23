using System;
using System.Collections.Generic;
using System.IO;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public abstract class AudioManager
    {
        private static float _globalSpeed = 1f;

        private bool _disposed;
        private List<StemMixer> _activeMixers = new();

        protected internal readonly SampleChannel[]     SfxSamples     = new SampleChannel[AudioHelpers.SfxPaths.Count];
        protected internal readonly DrumSampleChannel[] DrumSfxSamples = new DrumSampleChannel[AudioHelpers.DrumSfxPaths.Count];
        protected internal int PlaybackLatency;
        protected internal int MinimumBufferLength;
        protected internal int MaximumBufferLength;

        protected internal abstract ReadOnlySpan<string> SupportedFormats { get; }

        internal StemMixer? LoadCustomFile(string name, Stream stream, float speed, double volume, SongStem stem = SongStem.Song)
        {
            YargLogger.LogDebug("Loading custom audio file");
            var mixer = CreateMixer(name, stream, speed, volume, false);
            if (mixer == null)
            {
                return null;
            }

            if (!mixer.AddChannel(stem))
            {
                mixer.Dispose();
                return null;
            }
            YargLogger.LogDebug("Custom audio file loaded");
            return mixer;
        }

        internal StemMixer? LoadCustomFile(string file, float speed, double volume, SongStem stem = SongStem.Song)
        {
            var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 1);
            var mixer = LoadCustomFile(file, stream, speed, volume, stem);
            if (mixer == null)
            {
                YargLogger.LogFormatError("Failed to load audio file{0}!", file);
                stream.Dispose();
                return null;
            }
            return mixer;
        }

        protected internal abstract StemMixer? CreateMixer(string name, float speed, double volume, bool clampStemVolume);

        protected internal abstract StemMixer? CreateMixer(string name, Stream stream, float speed, double volume, bool clampStemVolume);

        protected internal abstract MicDevice? GetInputDevice(string name);

        protected internal abstract List<(int id, string name)> GetAllInputDevices();

        protected internal abstract MicDevice? CreateDevice(int deviceId, string name);

        protected internal abstract void SetMasterVolume(double volume);

        internal void ToggleBuffer(bool enable)
        {
            ToggleBuffer_Internal(enable);
            lock (_activeMixers)
            {
                foreach (var mixer in _activeMixers)
                {
                    mixer.ToggleBuffer(enable);
                }
            }
        }

        internal void SetBufferLength(int length)
        {
            SetBufferLength_Internal(length);
            lock (_activeMixers)
            {
                foreach (var mixer in _activeMixers)
                {
                    mixer.SetBufferLength(length);
                }
            }
        }

        protected abstract void ToggleBuffer_Internal(bool enable);

        protected abstract void SetBufferLength_Internal(int length);

        internal float GlobalSpeed
        {
            get => _globalSpeed;
            set
            {
                if (_disposed || _globalSpeed == value)
                {
                    return;
                }

                _globalSpeed = value;
                lock (_activeMixers)
                {
                    foreach (var mixer in _activeMixers)
                    {
                        mixer.SetSpeed(value, true);
                    }
                }
            }
        }

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void AddMixer(StemMixer mixer)
        {
            lock (this)
            {
                if (_disposed)
                {
                    mixer.Dispose();
                    return;
                }

                lock (_activeMixers)
                {
                    var level = GlobalAudioHandler.LogMixerStatus ? LogLevel.Debug : LogLevel.Trace;
                    YargLogger.LogFormat(level, "Mixer \"{0}\" created", mixer.Name);
                    _activeMixers.Add(mixer);
                }
            }
        }

        /// <summary>
        /// Communicates to the manager that the mixer is already disposed of.
        /// </summary>
        /// <remarks>Should stay limited to the Audio namespace</remarks>
        internal void RemoveMixer(StemMixer mixer)
        {
            lock (_activeMixers)
            {
                var level = GlobalAudioHandler.LogMixerStatus ? LogLevel.Debug : LogLevel.Trace;
                YargLogger.LogFormat(level, "Mixer \"{0}\" disposed", mixer.Name);
                _activeMixers.Remove(mixer);
            }
        }

        protected virtual void DisposeManagedResources() { }
        protected virtual void DisposeUnmanagedResources() { }

        private void Dispose(bool disposing)
        {
            lock (this)
            {
                if (!_disposed)
                {
                    StemMixer[] mixers;
                    lock (_activeMixers)
                    {
                        mixers = _activeMixers.ToArray();
                    }

                    foreach (var mixer in mixers)
                    {
                        mixer.Dispose();
                    }

                    foreach (var sample in SfxSamples)
                    {
                        sample?.Dispose();
                    }

                    foreach (var sample in DrumSfxSamples)
                    {
                        sample?.Dispose();
                    }

                    if (disposing)
                    {
                        DisposeManagedResources();
                    }
                    DisposeUnmanagedResources();
                    _disposed = true;
                }
            }
        }

        ~AudioManager()
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
