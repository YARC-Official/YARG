#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using ManagedBass;
using ManagedBass.Wasapi;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Wasapi
{
    /// <summary>
    ///     Handles low-latency audio playback and microphone routing through WASAPI in Exclusive mode,
    ///     feeding an output decode mixer directly into the WASAPI endpoint buffer.
    /// </summary>
    internal sealed class BassWasapiOutput : BassOutput
    {
        internal const string DEVICE_PREFIX = "WASAPI: ";

        private const int   DEFAULT_SAMPLE_RATE           = 48_000;
        private const int   DEFAULT_CHANNEL_COUNT         = 2;
        private const int   MINIMUM_READ_BLOCK_FRAMES     = 128;
        private const float DEFAULT_BUFFER_LENGTH_SECONDS = 0.05f;
        private const WasapiInitFlags INIT_FLAGS = WasapiInitFlags.Exclusive |
            WasapiInitFlags.EventDriven |
            WasapiInitFlags.AutoFormat |
            WasapiInitFlags.Buffer;

        private readonly BassWasapiMicManager _microphones;
        private readonly int                  _wasapiDeviceIndex;
        private volatile bool                 _isStarted;
        private volatile int                  _latencyFrames;
        private          int                  _restartQueued;
        private          double               _volume = 1;

        private BassWasapiOutput(string name, BassOutputDevice device, int wasapiDeviceIndex,
            BassWasapiMicManager microphones)
            : base(name, device)
        {
            _wasapiDeviceIndex = wasapiDeviceIndex;
            _microphones = microphones;
        }

        public override int HeardLatencyMilliseconds =>
            SampleRate > 0 ? (int) Math.Round(_latencyFrames * 1000.0 / SampleRate) : 0;

        internal override int EndpointDelayFrames => _latencyFrames;

        internal override double SongPlaybackStartDelay => SampleRate > 0 ? _latencyFrames / (double) SampleRate : 0;

        internal override bool UsesIndependentClock => true;

        internal static bool IsWasapiDevice(string name) => name.StartsWith(DEVICE_PREFIX, StringComparison.Ordinal);

        public static BassWasapiOutput? Find(string name, BassWasapiMicManager microphones)
        {
            if (!IsWasapiDevice(name))
            {
                return null;
            }

            foreach ((int id, string devName) in GetDevices())
            {
                if (string.Equals(devName, name, StringComparison.Ordinal))
                {
                    var device = BassOutputDevice.CreateWasapi(name);
                    return device == null ? null : new BassWasapiOutput(name, device, id, microphones);
                }
            }

            return null;
        }

        public static List<(int id, string name)> GetDevices()
        {
            var devices = new List<(int id, string name)>();
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsUsableOutput())
                    {
                        devices.Add((i, info.GetOutputDisplayName()));
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to enumerate WASAPI output devices");
            }

            return devices;
        }

        public override bool Start()
        {
            try
            {
                Device.Use();

                if (!BassWasapi.GetDeviceInfo(_wasapiDeviceIndex, out var deviceInfo))
                {
                    YargLogger.LogFormatError("Failed to get WASAPI device info for index {0}: {1}",
                        _wasapiDeviceIndex, Bass.LastError);
                    return false;
                }

                int sampleRate = deviceInfo.MixFrequency > 0 ? deviceInfo.MixFrequency : DEFAULT_SAMPLE_RATE;
                int channelCount = deviceInfo.MixChannels > 0 ? deviceInfo.MixChannels : DEFAULT_CHANNEL_COUNT;
                int minBlockFrames = Math.Max(MINIMUM_READ_BLOCK_FRAMES,
                    (int) Math.Round(deviceInfo.DefaultUpdatePeriod * sampleRate));

                if (!CreateOutputGraph(sampleRate, channelCount, BassFlags.Decode, minBlockFrames))
                {
                    return false;
                }

                float bufferLength = deviceInfo.DefaultUpdatePeriod > 0
                    ? (float) deviceInfo.DefaultUpdatePeriod
                    : DEFAULT_BUFFER_LENGTH_SECONDS;

                if (!BassWasapi.InitEx(
                    _wasapiDeviceIndex,
                    sampleRate,
                    channelCount,
                    INIT_FLAGS,
                    bufferLength,
                    0,
                    BassWasapi.WasapiProc_Bass,
                    (IntPtr) OutputMixerHandle))
                {
                    YargLogger.LogFormatError("Failed to initialize WASAPI device [{0}]: {1}", Name, Bass.LastError);
                    StopOutput();
                    return false;
                }

                BassWasapi.CurrentDevice = _wasapiDeviceIndex;

                if (BassWasapi.GetInfo(out var wasapiInfo))
                {
                    _latencyFrames = wasapiInfo.GetBufferFrames();
                    YargLogger.LogFormatInfo(
                        "WASAPI Exclusive output initialized: {0} Hz, {1} ch, buffer {2} bytes ({3} format)",
                        wasapiInfo.Frequency, wasapiInfo.Channels, wasapiInfo.BufferLength, wasapiInfo.Format);
                }

                if (!BassWasapi.Start())
                {
                    YargLogger.LogFormatError("Failed to start WASAPI output: {0}", Bass.LastError);
                    StopOutput();
                    return false;
                }

                _isStarted = true;
                SetVolume(_volume);
                if (!_microphones.AttachOutput(this))
                {
                    StopOutput();
                    return false;
                }

                return true;
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to start WASAPI Exclusive output");
                StopOutput();
                return false;
            }
        }

        public override OutputBufferInfo? GetBufferInfo() =>
            _isStarted ? new OutputBufferInfo(Array.Empty<int>(), _latencyFrames, SampleRate, true) : null;

        public override IReadOnlyList<InputDeviceInfo> GetInputs() => _microphones.GetAllDevices();

        public override MicDevice? CreateInput(InputDeviceInfo input) => _microphones.CreateMic(input);

        public override void SetVolume(double volume)
        {
            _volume = volume;
            if (!_isStarted)
            {
                return;
            }

            try
            {
                BassWasapi.CurrentDevice = _wasapiDeviceIndex;
                BassWasapi.SetVolume(WasapiVolumeTypes.Device | WasapiVolumeTypes.LogaritmicCurve, (float) volume);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to set WASAPI output volume");
            }
        }

        protected override void StopOutput()
        {
            _isStarted = false;
            _latencyFrames = 0;
            _microphones.DetachOutput(this);
            try
            {
                if (BassWasapi.GetDeviceInfo(_wasapiDeviceIndex, out var devInfo) && devInfo.IsInitialized)
                {
                    BassWasapi.CurrentDevice = _wasapiDeviceIndex;
                    BassWasapi.Stop();
                    BassWasapi.Free();
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Error stopping WASAPI device");
            }

            FreeOutputGraph();
        }

        internal void OnWasapiNotify(WasapiNotificationType notify, int device)
        {
            if ((device == _wasapiDeviceIndex || device == -1) && notify is WasapiNotificationType.Disabled)
            {
                if (Interlocked.Exchange(ref _restartQueued, 1) == 0)
                {
                    UnityMainThreadCallback.QueueEvent(RestartOutput);
                }
            }
        }

        private void RestartOutput()
        {
            Interlocked.Exchange(ref _restartQueued, 0);
            if (!IsDisposed)
            {
                YargLogger.LogInfo("Reinitializing WASAPI Exclusive output");
                RequestRestart();
            }
        }
    }

    internal static class BassWasapiOutputExtensions
    {
        public static bool IsUsableOutput(this WasapiDeviceInfo info) =>
            info.IsEnabled && !info.IsInput && !info.IsLoopback;

        public static string GetOutputDisplayName(this WasapiDeviceInfo info) =>
            $"{BassWasapiOutput.DEVICE_PREFIX}{info.Name}";
    }
}