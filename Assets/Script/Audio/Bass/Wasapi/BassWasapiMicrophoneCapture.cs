#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Wasapi;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Wasapi
{
    internal sealed class BassWasapiMicrophoneCapture : IDisposable
    {
        private const float             DEFAULT_BUFFER_LENGTH_SECONDS = 0.05f;
        private const WasapiInitFlags   COMMON_INIT_FLAGS            = WasapiInitFlags.EventDriven |
                                                                      WasapiInitFlags.AutoFormat |
                                                                      WasapiInitFlags.Buffer;

        private static readonly object _systemLock = new();

        private readonly int                   _deviceId;
        private readonly HashSet<int>          _claimedChannels = new();
        private readonly object                _stateLock       = new();

        private volatile bool _disposed;
        private volatile bool _running;
        private int           _listenerCount;
        private bool          _recordingRequested;

        private BassWasapiMicrophoneCapture(int deviceId, int channels, int sampleRate, int pushStreamHandle)
        {
            _deviceId = deviceId;
            Channels = channels;
            SampleRate = sampleRate;
            ReadHandle = pushStreamHandle;
        }

        public int  SampleRate  { get; }
        public int  Channels    { get; }
        public int  ReadHandle  { get; }
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
            BassWasapiMicrophoneCapture? capture = null;
            int pushStreamHandle = 0;
            lock (_systemLock)
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

                    pushStreamHandle = Bass.CreateStream(sampleRate, actualChannels,
                        BassFlags.Float | BassFlags.Decode, StreamProcedureType.Push);
                    if (pushStreamHandle == 0)
                    {
                        YargLogger.LogFormatError("Failed to create push stream for WASAPI mic device {0}: {1}",
                            deviceId, Bass.LastError);
                        return null;
                    }

                    capture = new BassWasapiMicrophoneCapture(deviceId, actualChannels, sampleRate, pushStreamHandle);
                    if (!capture.InitializeDevice(deviceInfo))
                    {
                        capture.Dispose();
                        capture = null;
                        return null;
                    }

                    return capture;
                }
                catch (Exception exception)
                {
                    capture?.Dispose();
                    if (capture == null && pushStreamHandle != 0)
                    {
                        Bass.StreamFree(pushStreamHandle);
                    }

                    YargLogger.LogException(exception, $"Failed to create WASAPI microphone capture for device {deviceId}");
                    return null;
                }
            }
        }

        private bool InitializeDevice(WasapiDeviceInfo deviceInfo)
        {
            float bufferLength = deviceInfo.DefaultUpdatePeriod > 0
                ? (float) deviceInfo.DefaultUpdatePeriod
                : DEFAULT_BUFFER_LENGTH_SECONDS;

            if (TryInitializeDevice(deviceInfo, bufferLength, WasapiInitFlags.Exclusive))
            {
                return true;
            }

            var exclusiveError = Bass.LastError;
            YargLogger.LogFormatWarning(
                "Failed to initialize WASAPI input device [{0}] in Exclusive mode ({1}), attempting Shared mode fallback...",
                _deviceId, exclusiveError);

            if (TryInitializeDevice(deviceInfo, bufferLength, WasapiInitFlags.Shared))
            {
                return true;
            }

            YargLogger.LogFormatError("Failed to initialize WASAPI input device [{0}] in both Exclusive and Shared modes: {1}",
                _deviceId, Bass.LastError);
            return false;
        }

        private bool TryInitializeDevice(WasapiDeviceInfo deviceInfo, float bufferLength, WasapiInitFlags mode)
        {
            var flags = mode | COMMON_INIT_FLAGS;
            if (!BassWasapi.InitEx(_deviceId, SampleRate, Channels, flags, bufferLength, 0,
                    BassWasapi.WasapiProc_Bass, (IntPtr) ReadHandle))
            {
                return false;
            }

            BassWasapi.CurrentDevice = _deviceId;
            string modeName = mode == WasapiInitFlags.Exclusive ? "Exclusive" : "Shared";
            if (BassWasapi.GetInfo(out var info) && info.Channels > 0 && info.Frequency > 0)
            {
                int bufferFrames = info.BufferLength / (info.Channels * sizeof(float));
                double bufferMilliseconds = bufferFrames * 1000.0 / info.Frequency;
                YargLogger.LogFormatInfo(
                    "WASAPI {0} input initialized: device [{1}] ({2}), {3} Hz, {4} ch, requested buffer {5:F3}s, actual buffer {6} bytes ({7} frames, {8:F3}ms)",
                    modeName, _deviceId, deviceInfo.Name, SampleRate, Channels, bufferLength, info.BufferLength,
                    bufferFrames, bufferMilliseconds);
            }
            else
            {
                YargLogger.LogFormatInfo(
                    "WASAPI {0} input initialized: device [{1}] ({2}), {3} Hz, {4} ch, requested buffer {5:F3}s, actual buffer unavailable",
                    modeName, _deviceId, deviceInfo.Name, SampleRate, Channels, bufferLength);
            }

            return true;
        }

        public bool TryClaimChannel(int channel)
        {
            lock (_stateLock)
            {
                if (_disposed || channel < 0 || channel >= Channels)
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
                if (_disposed)
                {
                    return false;
                }

                bool paused = PauseCapture();
                bool discarded = DiscardBufferedAudio();
                return paused && discarded;
            }
        }

        public bool Resume()
        {
            lock (_stateLock)
            {
                return !_disposed && StartIfNeeded();
            }
        }

        internal static bool SetNotification(WasapiNotifyProcedure? procedure)
        {
            lock (_systemLock)
            {
                return BassWasapi.SetNotify(procedure, IntPtr.Zero);
            }
        }

        private bool DiscardBufferedAudio()
        {
            int waitingBytes = Bass.ChannelGetData(ReadHandle, IntPtr.Zero, (int) DataFlags.Available);
            if (waitingBytes < 0)
            {
                return false;
            }

            if (waitingBytes == 0)
            {
                return true;
            }

            return Bass.ChannelGetData(ReadHandle, IntPtr.Zero, waitingBytes) == waitingBytes;
        }

        public MicBufferInfo GetBufferInfo()
        {
            var buffer = GetWasapiBuffer();

            return new MicBufferInfo(
                bufferFrames: buffer.Frames,
                bufferMilliseconds: buffer.Milliseconds,
                sampleRate: SampleRate,
                channels: Channels,
                isAsio: false,
                cushionMilliseconds: 0,
                waitingBytes: GetWaitingBytes());
        }

        private (int Frames, double Milliseconds) GetWasapiBuffer()
        {
            lock (_systemLock)
            {
                try
                {
                    if (!BassWasapi.GetDeviceInfo(_deviceId, out var deviceInfo) || !deviceInfo.IsInitialized)
                    {
                        return (0, 0);
                    }

                    BassWasapi.CurrentDevice = _deviceId;
                    if (!BassWasapi.GetInfo(out var info) || info.Channels <= 0 || info.Frequency <= 0)
                    {
                        return (0, 0);
                    }

                    int frames = info.BufferLength / (info.Channels * sizeof(float));
                    double milliseconds = frames * 1000.0 / info.Frequency;
                    return (frames, milliseconds);
                }
                catch
                {
                    return (0, 0);
                }
            }
        }

        private int GetWaitingBytes()
        {
            if (ReadHandle == 0)
            {
                return 0;
            }

            return Math.Max(0, Bass.ChannelGetData(ReadHandle, IntPtr.Zero, (int) DataFlags.Available));
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

            lock (_systemLock)
            {
                try
                {
                    if (BassWasapi.GetDeviceInfo(_deviceId, out var devInfo) && devInfo.IsInitialized)
                    {
                        BassWasapi.CurrentDevice = _deviceId;
                        BassWasapi.Stop(true);
                        BassWasapi.Free();
                    }
                }
                catch (Exception exception)
                {
                    YargLogger.LogException(exception, $"Error freeing WASAPI device {_deviceId}");
                }

                if (ReadHandle != 0)
                {
                    Bass.StreamFree(ReadHandle);
                }
            }
        }

        private bool StartIfNeeded()
        {
            if (!ShouldRun || _running)
            {
                return true;
            }

            lock (_systemLock)
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
            }
        }

        private bool PauseCapture()
        {
            if (!_running)
            {
                return true;
            }

            lock (_systemLock)
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
            }
        }

        internal void OnWasapiNotify(WasapiNotificationType notify, int device)
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
                    lock (_systemLock)
                    {
                        try
                        {
                            BassWasapi.CurrentDevice = _deviceId;
                            BassWasapi.Stop(true);
                            BassWasapi.Start();
                        }
                        catch (Exception exception)
                        {
                            YargLogger.LogException(exception, $"Failed to restart WASAPI device {_deviceId}");
                        }
                    }
                }
            }
        }
    }
}
