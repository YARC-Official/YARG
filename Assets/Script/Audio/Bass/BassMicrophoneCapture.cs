#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Mix;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    /// <summary>
    ///     Captures audio from a standard BASS recording channel without a managed recording callback.
    /// </summary>
    internal sealed class BassMicrophoneCapture : IDisposable
    {
        private static readonly int[] PreferredSampleRates =
        {
            48000,
            44100,
            96000,
            16000,
        };

        private static readonly object SystemLock = new();

        private readonly int              _microphoneHandle;
        private readonly int              _deviceId;
        private readonly HashSet<int>     _claimedChannels = new();
        private readonly object           _stateLock       = new();
        private readonly bool             _ownsDevice;

        private volatile bool _disposed;
        private volatile bool _running;
        private int           _listenerCount;
        private bool          _recordingRequested;

        private BassMicrophoneCapture(int deviceId, int channels, int microphoneHandle,
            bool ownsDevice, int sampleRate)
        {
            _deviceId = deviceId;
            Channels = channels;
            _microphoneHandle = microphoneHandle;
            _ownsDevice = ownsDevice;
            SampleRate = sampleRate;
        }

        public int SampleRate { get; }
        public int Channels   { get; }
        public int ReadHandle => _microphoneHandle;

        public MicBufferInfo GetBufferInfo()
        {
            int devicePeriod = Math.Max(0, Bass.GetConfig(Configuration.DevicePeriod));
            int bufferFrames = (int) Math.Round(SampleRate * (devicePeriod / 1000.0));
            int waitingBytes = GetAvailableRecordBytes();

            return new MicBufferInfo(
                bufferFrames: bufferFrames,
                bufferMilliseconds: devicePeriod,
                sampleRate: SampleRate,
                channels: Channels,
                isAsio: false,
                cushionMilliseconds: 0,
                waitingBytes: waitingBytes);
        }

        private bool ShouldRun => _recordingRequested && _listenerCount > 0 && !_disposed;

        internal bool HasClaimedChannel
        {
            get
            {
                lock (_stateLock)
                {
                    return _claimedChannels.Count > 0;
                }
            }
        }

        public void Dispose()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                _recordingRequested = false;
                _claimedChannels.Clear();
            }

            WithSystemLock(() =>
            {
                Bass.ChannelStop(_microphoneHandle);
                Bass.StreamFree(_microphoneHandle);
                if (_ownsDevice)
                {
                    FreeOwnedDevice(_deviceId);
                }
            });
        }

        internal static void WithSystemLock(Action action)
        {
            lock (SystemLock)
            {
                action();
            }
        }

        internal static T WithSystemLock<T>(Func<T> func)
        {
            lock (SystemLock)
            {
                return func();
            }
        }

        public static BassMicrophoneCapture? Create(int deviceId, int channels)
        {
            return WithSystemLock(() =>
            {
                bool ownsDevice = Bass.RecordInit(deviceId);
                if (!ownsDevice && Bass.LastError != Errors.Already)
                {
                    return null;
                }

                Bass.CurrentRecordingDevice = deviceId;
                int devicePeriod = Bass.GetConfig(Configuration.DevicePeriod);
                var capture = CreateCapture(deviceId, channels, devicePeriod, ownsDevice);
                if (capture != null)
                {
                    return capture;
                }

                if (ownsDevice)
                {
                    FreeOwnedDevice(deviceId);
                }

                YargLogger.LogFormatError(
                    "Failed to create recording for device [{0}] at any supported sample rate",
                    deviceId);
                return null;
            });
        }

        private static BassMicrophoneCapture? CreateCapture(int deviceId, int channels, int devicePeriod,
            bool ownsDevice)
        {
            foreach (int sampleRate in PreferredSampleRates)
            {
                var capture = CreateAtRate(deviceId, channels, sampleRate, devicePeriod, BassFlags.Float,
                    ownsDevice);
                if (capture != null)
                {
                    return capture;
                }

                capture = CreateAtRate(deviceId, channels, sampleRate, devicePeriod, BassFlags.Default,
                    ownsDevice);
                if (capture != null)
                {
                    return capture;
                }
            }

            return null;
        }

        private static BassMicrophoneCapture? CreateAtRate(int deviceId, int channels, int sampleRate,
            int devicePeriod, BassFlags captureFlags, bool ownsDevice)
        {
            bool isFloatCapture = captureFlags.HasFlag(BassFlags.Float);
            int microphoneHandle = Bass.RecordStart(sampleRate, channels, captureFlags | BassFlags.RecordPause,
                devicePeriod, null, IntPtr.Zero);
            if (microphoneHandle == 0)
            {
                YargLogger.LogFormatTrace("Failed to create recording at {0} Hz / {1} ch ({2}): {3}",
                    sampleRate, channels, isFloatCapture ? "float" : "native format",
                    Bass.LastError);
                return null;
            }

            return new BassMicrophoneCapture(deviceId, channels, microphoneHandle, ownsDevice, sampleRate);
        }

        private int GetAvailableRecordBytes()
        {
            int waitingBytes = Bass.ChannelGetData(_microphoneHandle, IntPtr.Zero, (int) DataFlags.Available);
            return Math.Max(0, waitingBytes);
        }

        public bool Start()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return false;
                }

                _recordingRequested = true;
                return StartIfNeeded();
            }
        }

        private bool StartIfNeeded()
        {
            if (!ShouldRun || IsCaptureRunning())
            {
                return true;
            }

            if (!Bass.ChannelPlay(_microphoneHandle))
            {
                YargLogger.LogFormatError("Failed to start recording for device [{0}]: {1}", _deviceId,
                    Bass.LastError);
                return false;
            }

            _running = true;
            return true;
        }

        private bool PauseCapture()
        {
            if (!IsCaptureRunning())
            {
                return true;
            }

            if (!Bass.ChannelPause(_microphoneHandle))
            {
                var error = Bass.LastError;
                if (error == Errors.NotPlaying)
                {
                    _running = false;
                    return true;
                }

                YargLogger.LogFormatError("Failed to pause recording for device [{0}]: {1}", _deviceId, error);
                return false;
            }

            _running = false;
            return true;
        }

        internal bool PauseAndDiscardBufferedAudio()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return false;
                }

                return PauseAndDiscardCapture();
            }
        }

        internal bool Resume()
        {
            lock (_stateLock)
            {
                return !_disposed && StartIfNeeded();
            }
        }

        private bool PauseAndDiscardCapture()
        {
            bool paused = PauseCapture();
            bool discarded = DiscardAudioLocked();
            return discarded && paused;
        }

        private bool DiscardAudioLocked()
        {
            int waitingBytes = GetAvailableRecordBytes();
            if (waitingBytes <= 0)
            {
                return true;
            }

            var buffer = new byte[Math.Min(waitingBytes, 4096)];
            while (waitingBytes > 0)
            {
                int toRead = Math.Min(waitingBytes, buffer.Length);
                int read = Bass.ChannelGetData(_microphoneHandle, buffer, toRead);
                if (read <= 0)
                {
                    break;
                }

                waitingBytes -= read;
            }

            BassMix.SplitStreamReset(_microphoneHandle, 0);
            return true;
        }

        internal void AddListener()
        {
            lock (_stateLock)
            {
                ++_listenerCount;
                if (!StartIfNeeded())
                {
                    YargLogger.LogFormatError("Failed to resume recording after monitor attach for device [{0}]",
                        _deviceId);
                }
            }
        }

        internal void RemoveListener()
        {
            lock (_stateLock)
            {
                if (_disposed)
                {
                    return;
                }

                --_listenerCount;

                if (_listenerCount == 0)
                {
                    PauseAndDiscardCapture();
                }
            }
        }

        private bool IsCaptureRunning()
        {
            if (!_running)
            {
                return false;
            }

            _running = Bass.ChannelIsActive(_microphoneHandle) == PlaybackState.Playing;
            return _running;
        }

        internal bool TryClaimChannel(int channel)
        {
            lock (_stateLock)
            {
                if (_disposed || !_claimedChannels.Add(channel))
                {
                    return false;
                }

                return true;
            }
        }

        internal void ReleaseChannel(int channel)
        {
            lock (_stateLock)
            {
                _claimedChannels.Remove(channel);
            }
        }

        internal bool IsChannelClaimed(int channel)
        {
            lock (_stateLock)
            {
                return _claimedChannels.Contains(channel);
            }
        }

        private static void FreeOwnedDevice(int deviceId)
        {
            if (!Bass.RecordGetDeviceInfo(deviceId, out var info) || !info.IsInitialized)
            {
                return;
            }

            Bass.CurrentRecordingDevice = deviceId;
            if (!Bass.RecordFree())
            {
                YargLogger.LogFormatWarning("Failed to free recording device [{0}]: {1}", deviceId, Bass.LastError);
            }
        }
    }
}
