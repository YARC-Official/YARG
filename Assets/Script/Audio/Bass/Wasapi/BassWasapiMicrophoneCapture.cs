#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Wasapi;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Wasapi
{
    /// <summary>
    ///     Captures audio from a WASAPI Exclusive recording device, pushing hardware-timed sample packets
    ///     into a BASS push stream for low-latency pitch detection and vocal monitoring.
    /// </summary>
    internal sealed class BassWasapiMicrophoneCapture : IDisposable
    {
        private const float DEFAULT_BUFFER_LENGTH_SECONDS = 0.05f;

        private static readonly object SystemLock = new();

        private readonly int                    _deviceId;
        private readonly int                    _sampleRate;
        private readonly int                    _channels;
        private readonly int                    _pushStreamHandle;
        private readonly WasapiNotifyProcedure  _notifyProcedure;
        private readonly HashSet<int>           _claimedChannels = new();
        private readonly object                 _stateLock       = new();

        private volatile bool _disposed;
        private volatile bool _running;
        private int           _listenerCount;
        private bool          _recordingRequested;
        private bool          _isExclusive;

        private BassWasapiMicrophoneCapture(int deviceId, int channels, int sampleRate, int pushStreamHandle)
        {
            _deviceId = deviceId;
            _channels = channels;
            _sampleRate = sampleRate;
            _pushStreamHandle = pushStreamHandle;
            _notifyProcedure = OnWasapiNotify;
        }

        public int SampleRate => _sampleRate;
        public int Channels   => _channels;
        public int ReadHandle => _pushStreamHandle;

        public bool IsExclusive => _isExclusive;

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

        public static BassWasapiMicrophoneCapture? Create(int deviceId, int channels)
        {
            return WithSystemLock(() =>
            {
                try
                {
                    if (!BassWasapi.GetDeviceInfo(deviceId, out var deviceInfo) || !deviceInfo.IsEnabled || !deviceInfo.IsInput)
                    {
                        YargLogger.LogFormatError("Invalid WASAPI input device info for index {0}", deviceId);
                        return null;
                    }

                    int sampleRate = deviceInfo.MixFrequency > 0 ? deviceInfo.MixFrequency : 48_000;
                    int actualChannels = channels > 0 ? channels : (deviceInfo.MixChannels > 0 ? deviceInfo.MixChannels : 1);

                    int pushStreamHandle = Bass.CreateStream(sampleRate, actualChannels,
                        BassFlags.Float | BassFlags.Decode, StreamProcedureType.Push);
                    if (pushStreamHandle == 0)
                    {
                        YargLogger.LogFormatError("Failed to create push stream for WASAPI mic device {0}: {1}",
                            deviceId, Bass.LastError);
                        return null;
                    }

                    var capture = new BassWasapiMicrophoneCapture(deviceId, actualChannels, sampleRate, pushStreamHandle);
                    if (!capture.InitializeDevice(deviceInfo))
                    {
                        capture.Dispose();
                        return null;
                    }

                    return capture;
                }
                catch (Exception exception)
                {
                    YargLogger.LogException(exception, $"Failed to create WASAPI microphone capture for device {deviceId}");
                    return null;
                }
            });
        }

        private bool InitializeDevice(WasapiDeviceInfo deviceInfo)
        {
            float bufferLength = deviceInfo.DefaultUpdatePeriod > 0
                ? (float) deviceInfo.DefaultUpdatePeriod
                : DEFAULT_BUFFER_LENGTH_SECONDS;

            // Try Exclusive mode first for lowest latency
            var exclusiveFlags = WasapiInitFlags.Exclusive | WasapiInitFlags.EventDriven |
                                 WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer;

            bool initialized = BassWasapi.InitEx(_deviceId, _sampleRate, _channels, exclusiveFlags, bufferLength, 0,
                BassWasapi.WasapiProc_Bass, (IntPtr) _pushStreamHandle);

            if (initialized)
            {
                BassWasapi.CurrentDevice = _deviceId;
                BassWasapi.SetNotify(_notifyProcedure, IntPtr.Zero);
                _isExclusive = true;
                YargLogger.LogFormatInfo(
                    "WASAPI Exclusive input initialized: device [{0}] ({1}), {2} Hz, {3} ch, buffer {4:F3}s",
                    _deviceId, deviceInfo.Name, _sampleRate, _channels, bufferLength);
                return true;
            }

            var exclusiveError = Bass.LastError;
            YargLogger.LogFormatWarning(
                "Failed to initialize WASAPI input device [{0}] in Exclusive mode ({1}), attempting Shared mode fallback...",
                _deviceId, exclusiveError);

            // Fallback to Shared mode if Exclusive is unavailable or locked
            var sharedFlags = WasapiInitFlags.Shared | WasapiInitFlags.EventDriven |
                              WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer;

            if (BassWasapi.InitEx(_deviceId, _sampleRate, _channels, sharedFlags, bufferLength, 0,
                    BassWasapi.WasapiProc_Bass, (IntPtr) _pushStreamHandle))
            {
                BassWasapi.CurrentDevice = _deviceId;
                BassWasapi.SetNotify(_notifyProcedure, IntPtr.Zero);
                _isExclusive = false;
                YargLogger.LogFormatInfo(
                    "WASAPI Shared input initialized (fallback): device [{0}] ({1}), {2} Hz, {3} ch, buffer {4:F3}s",
                    _deviceId, deviceInfo.Name, _sampleRate, _channels, bufferLength);
                return true;
            }

            YargLogger.LogFormatError("Failed to initialize WASAPI input device [{0}] in both Exclusive and Shared modes: {1}",
                _deviceId, Bass.LastError);
            return false;
        }

        public bool TryClaimChannel(int channel)
        {
            lock (_stateLock)
            {
                if (_disposed || channel < 0 || channel >= _channels)
                {
                    return false;
                }

                return _claimedChannels.Add(channel);
            }
        }

        public void ReleaseChannel(int channel)
        {
            lock (_stateLock)
            {
                _claimedChannels.Remove(channel);
            }
        }

        public bool IsChannelClaimed(int channel)
        {
            lock (_stateLock)
            {
                return _claimedChannels.Contains(channel);
            }
        }

        public void AddListener()
        {
            lock (_stateLock)
            {
                _listenerCount++;
                StartIfNeeded();
            }
        }

        public void RemoveListener()
        {
            lock (_stateLock)
            {
                if (_listenerCount > 0)
                {
                    _listenerCount--;
                }

                if (!ShouldRun)
                {
                    PauseCapture();
                }
            }
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

        public bool PauseAndDiscardBufferedAudio()
        {
            lock (_stateLock)
            {
                PauseCapture();
                if (_pushStreamHandle != 0)
                {
                    Bass.ChannelSetPosition(_pushStreamHandle, 0);
                }
                return true;
            }
        }

        public bool Resume()
        {
            lock (_stateLock)
            {
                return StartIfNeeded();
            }
        }

        public MicBufferInfo GetBufferInfo()
        {
            int bufferFrames = 0;
            double bufferMs = 0;
            int waitingBytes = 0;

            WithSystemLock(() =>
            {
                try
                {
                    if (BassWasapi.GetDeviceInfo(_deviceId, out var devInfo) && devInfo.IsInitialized)
                    {
                        BassWasapi.CurrentDevice = _deviceId;
                        if (BassWasapi.GetInfo(out var info))
                        {
                            bufferFrames = (info.Channels > 0 && info.Frequency > 0)
                                ? (info.BufferLength / (info.Channels * sizeof(float)))
                                : 0;
                            bufferMs = info.Frequency > 0 ? (bufferFrames * 1000.0 / info.Frequency) : 0;
                        }
                    }
                }
                catch
                {
                }
            });

            if (_pushStreamHandle != 0)
            {
                waitingBytes = Math.Max(0, Bass.ChannelGetData(_pushStreamHandle, IntPtr.Zero, (int) DataFlags.Available));
            }

            return new MicBufferInfo(
                bufferFrames: bufferFrames,
                bufferMilliseconds: bufferMs,
                sampleRate: _sampleRate,
                channels: _channels,
                isAsio: false,
                cushionMilliseconds: 0,
                waitingBytes: waitingBytes);
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
                _running = false;
                _claimedChannels.Clear();
            }

            WithSystemLock(() =>
            {
                try
                {
                    if (BassWasapi.GetDeviceInfo(_deviceId, out var devInfo) && devInfo.IsInitialized)
                    {
                        BassWasapi.CurrentDevice = _deviceId;
                        BassWasapi.SetNotify(null, IntPtr.Zero);
                        BassWasapi.Stop(true);
                        BassWasapi.Free();
                    }
                }
                catch (Exception exception)
                {
                    YargLogger.LogException(exception, $"Error freeing WASAPI device {_deviceId}");
                }

                if (_pushStreamHandle != 0)
                {
                    Bass.StreamFree(_pushStreamHandle);
                }
            });
        }

        private bool StartIfNeeded()
        {
            if (!ShouldRun || _running)
            {
                return true;
            }

            return WithSystemLock(() =>
            {
                BassWasapi.CurrentDevice = _deviceId;
                if (!BassWasapi.Start())
                {
                    YargLogger.LogFormatError("Failed to start WASAPI recording for device [{0}]: {1}",
                        _deviceId, Bass.LastError);
                    return false;
                }

                _running = true;
                return true;
            });
        }

        private bool PauseCapture()
        {
            if (!_running)
            {
                return true;
            }

            return WithSystemLock(() =>
            {
                BassWasapi.CurrentDevice = _deviceId;
                if (!BassWasapi.Stop(false))
                {
                    var error = Bass.LastError;
                    if (error == Errors.NotPlaying)
                    {
                        _running = false;
                        return true;
                    }

                    YargLogger.LogFormatError("Failed to pause WASAPI recording for device [{0}]: {1}",
                        _deviceId, error);
                    return false;
                }

                _running = false;
                return true;
            });
        }

        private void OnWasapiNotify(WasapiNotificationType notify, int device, IntPtr user)
        {
            if (device != _deviceId && device != -1)
            {
                return;
            }

            if (notify is WasapiNotificationType.Disabled)
            {
                YargLogger.LogFormatWarning("WASAPI input device [{0}] disabled or disconnected", _deviceId);
                if (_running && !_disposed)
                {
                    WithSystemLock(() =>
                    {
                        try
                        {
                            BassWasapi.CurrentDevice = _deviceId;
                            BassWasapi.Stop(true);
                            BassWasapi.Start();
                        }
                        catch
                        {
                        }
                    });
                }
            }
        }

        private static void WithSystemLock(Action action)
        {
            lock (SystemLock)
            {
                action();
            }
        }

        private static T WithSystemLock<T>(Func<T> func)
        {
            lock (SystemLock)
            {
                return func();
            }
        }
    }
}
