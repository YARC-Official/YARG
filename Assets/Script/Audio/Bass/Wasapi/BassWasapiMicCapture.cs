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
    ///     Captures audio from a WASAPI input device in Exclusive mode, feeding incoming samples directly
    ///     into a BASS push stream for real-time processing and analysis.
    /// </summary>
    internal sealed class BassWasapiMicCapture : IDisposable
    {
        private const int   DEFAULT_SAMPLE_RATE           = 48_000;
        private const int   DEFAULT_CHANNEL_COUNT         = 1;
        private const float DEFAULT_BUFFER_LENGTH_SECONDS = 0.05f;
        private const WasapiInitFlags INIT_FLAGS = WasapiInitFlags.Exclusive |
            WasapiInitFlags.EventDriven |
            WasapiInitFlags.AutoFormat |
            WasapiInitFlags.Buffer;

        private readonly HashSet<int> _claimedChannels = new();

        private bool _disposed;

        private BassWasapiMicCapture(int deviceId, int channels, int sampleRate, int pushStreamHandle,
            int bufferFrames, double bufferMilliseconds)
        {
            DeviceId = deviceId;
            Channels = channels;
            SampleRate = sampleRate;
            ReadHandle = pushStreamHandle;
            BufferFrames = bufferFrames;
            BufferMilliseconds = bufferMilliseconds;
        }

        public int    DeviceId           { get; }
        public int    SampleRate         { get; }
        public int    Channels           { get; }
        public int    ReadHandle         { get; }
        public int    BufferFrames       { get; }
        public double BufferMilliseconds { get; }
        public bool   IsRunning          { get; private set; }

        internal bool HasClaimedChannel => _claimedChannels.Count > 0;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            IsRunning = false;
            _claimedChannels.Clear();

            try
            {
                if (BassWasapi.GetDeviceInfo(DeviceId, out var devInfo) && devInfo.IsInitialized)
                {
                    BassWasapi.CurrentDevice = DeviceId;
                    BassWasapi.Stop();
                    BassWasapi.Free();
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Error freeing WASAPI device {DeviceId}");
            }

            if (ReadHandle != 0)
            {
                Bass.StreamFree(ReadHandle);
            }
        }

        public static BassWasapiMicCapture? Create(int deviceId, int channels)
        {
            try
            {
                if (!BassWasapi.GetDeviceInfo(deviceId, out var deviceInfo) || !deviceInfo.IsUsableInput())
                {
                    YargLogger.LogFormatError("Invalid WASAPI input device info for index {0}", deviceId);
                    return null;
                }

                int sampleRate = deviceInfo.MixFrequency > 0 ? deviceInfo.MixFrequency : DEFAULT_SAMPLE_RATE;
                int actualChannels = channels > 0 ? channels :
                    deviceInfo.MixChannels > 0    ? deviceInfo.MixChannels : DEFAULT_CHANNEL_COUNT;

                int pushStreamHandle = Bass.CreateStream(
                    sampleRate,
                    actualChannels,
                    BassFlags.Float | BassFlags.Decode,
                    StreamProcedureType.Push);
                if (pushStreamHandle == 0)
                {
                    YargLogger.LogFormatError("Failed to create push stream for WASAPI mic device {0}: {1}",
                        deviceId, Bass.LastError);
                    return null;
                }

                float bufferLength = deviceInfo.DefaultUpdatePeriod > 0
                    ? (float) deviceInfo.DefaultUpdatePeriod
                    : DEFAULT_BUFFER_LENGTH_SECONDS;

                if (!BassWasapi.InitEx(
                    deviceId,
                    sampleRate,
                    actualChannels,
                    INIT_FLAGS,
                    bufferLength,
                    0,
                    BassWasapi.WasapiProc_Bass,
                    (IntPtr) pushStreamHandle))
                {
                    YargLogger.LogFormatError("Failed to initialize WASAPI input device [{0}] in Exclusive mode: {1}",
                        deviceId, Bass.LastError);
                    Bass.StreamFree(pushStreamHandle);
                    return null;
                }

                BassWasapi.CurrentDevice = deviceId;
                int bufferFrames = 0;
                double bufferMs = 0.0;
                if (BassWasapi.GetInfo(out var info) && info.Channels > 0 && info.Frequency > 0)
                {
                    bufferFrames = info.GetBufferFrames();
                    bufferMs = info.GetBufferMilliseconds(bufferFrames);
                    YargLogger.LogFormatInfo(
                        "WASAPI Exclusive input initialized: device [{0}] ({1}), {2} Hz, {3} ch, buffer {4} bytes ({5} frames, {6:F3}ms)",
                        deviceId, deviceInfo.Name, sampleRate, actualChannels, info.BufferLength, bufferFrames,
                        bufferMs);
                }

                return new BassWasapiMicCapture(
                    deviceId,
                    actualChannels,
                    sampleRate,
                    pushStreamHandle,
                    bufferFrames,
                    bufferMs);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Failed to create WASAPI microphone capture for device {deviceId}");
                return null;
            }
        }

        public bool Start()
        {
            if (_disposed)
            {
                return false;
            }

            if (IsRunning)
            {
                return true;
            }

            BassWasapi.CurrentDevice = DeviceId;
            if (!BassWasapi.Start())
            {
                YargLogger.LogFormatError("Failed to start WASAPI recording for device [{0}]: {1}",
                    DeviceId, Bass.LastError);
                return false;
            }

            IsRunning = true;
            return true;
        }

        public bool TryClaimChannel(int channel) =>
            !_disposed && channel.IsValidChannel(Channels) && _claimedChannels.Add(channel);

        public void ReleaseChannel(int channel) => _claimedChannels.Remove(channel);

        public bool IsChannelClaimed(int channel) => _claimedChannels.Contains(channel);

        internal bool DiscardBufferedAudio() => ReadHandle == 0 || _disposed || Bass.ChannelSetPosition(ReadHandle, 0);

        public MicBufferInfo GetBufferInfo()
        {
            int waitingBytes = ReadHandle != 0
                ? Math.Max(0, Bass.ChannelGetData(ReadHandle, IntPtr.Zero, (int) DataFlags.Available))
                : 0;

            return new MicBufferInfo(
                BufferFrames,
                BufferMilliseconds,
                SampleRate,
                Channels,
                false,
                0,
                waitingBytes);
        }

        internal void OnWasapiNotify(WasapiNotificationType notify, int device)
        {
            if ((device == DeviceId || device == -1) && notify is WasapiNotificationType.Disabled)
            {
                YargLogger.LogFormatWarning("WASAPI input device [{0}] disabled or disconnected", DeviceId);
                IsRunning = false;
            }
        }
    }

    internal static class BassWasapiMicCaptureExtensions
    {
        public static bool IsValidChannel(this int channel, int totalChannels) =>
            channel >= 0 && channel < totalChannels;

        public static int GetBufferFrames(this WasapiInfo info) =>
            info.Channels > 0 ? info.BufferLength / (info.Channels * sizeof(float)) : 0;

        public static double GetBufferMilliseconds(this WasapiInfo info, int bufferFrames) =>
            info.Frequency > 0 ? bufferFrames * 1000.0 / info.Frequency : 0;
    }
}