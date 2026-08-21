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
    ///     Outputs audio using WASAPI Exclusive mode for low-latency playback.
    ///     The master audio stream is combined into a single decode mixer that BASSWASAPI pulls from directly.
    /// </summary>
    internal sealed class BassWasapiOutput : BassOutput
    {
        internal const string DEVICE_PREFIX = "WASAPI: ";

        private const int DEFAULT_SAMPLE_RATE = 48_000;
        private const int DEFAULT_CHANNEL_COUNT = 2;
        private const int MINIMUM_READ_BLOCK_FRAMES = 128;
        private const float DEFAULT_BUFFER_LENGTH_SECONDS = 0.05f; // 50 ms buffer

        private readonly int                   _wasapiDeviceIndex;
        private readonly BassAudioRouter       _router;
        private readonly BassWasapiMicManager  _microphones;
        private readonly WasapiNotifyProcedure _notifyProcedure;

        private double _volume = 1;
        private int    _restartQueued;
        private bool   _isStarted;

        private BassWasapiOutput(string name, BassOutputDevice device, int wasapiDeviceIndex, BassAudioRouter router)
            : base(name, device)
        {
            _wasapiDeviceIndex = wasapiDeviceIndex;
            _router = router;
            _microphones = new BassWasapiMicManager(router);
            _notifyProcedure = OnWasapiNotify;
        }

        public override int HeardLatencyMilliseconds =>
            (int) Math.Round(SongPlaybackStartDelay * 1000.0);

        internal override int EndpointDelayFrames
        {
            get
            {
                if (!_isStarted || ChannelCount <= 0)
                {
                    return 0;
                }

                try
                {
                    BassWasapi.CurrentDevice = _wasapiDeviceIndex;
                    int availableBytes = BassWasapi.GetData(IntPtr.Zero, (int) DataFlags.Available);
                    return availableBytes > 0 ? (availableBytes / (ChannelCount * sizeof(float))) : 0;
                }
                catch
                {
                    return 0;
                }
            }
        }

        internal override double SongPlaybackStartDelay =>
            SampleRate > 0 ? EndpointDelayFrames / (double) SampleRate : 0;

        internal override bool UsesIndependentClock => true;

        public static BassWasapiOutput? Find(string name, BassAudioRouter router)
        {
            if (!name.StartsWith(DEVICE_PREFIX, StringComparison.Ordinal))
            {
                return null;
            }

            string rawName = name.Substring(DEVICE_PREFIX.Length);
            int deviceIndex = FindDevice(rawName);
            if (deviceIndex < 0)
            {
                return null;
            }

            var device = BassOutputDevice.CreateWasapi(name);
            return device == null ? null : new BassWasapiOutput(name, device, deviceIndex, router);
        }

        public static List<(int id, string name)> GetDevices()
        {
            var devices = new List<(int id, string name)>();
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsEnabled && !info.IsInput && !info.IsLoopback)
                    {
                        devices.Add((i, $"{DEVICE_PREFIX}{info.Name}"));
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

                var flags = WasapiInitFlags.Exclusive | WasapiInitFlags.EventDriven |
                            WasapiInitFlags.AutoFormat | WasapiInitFlags.Buffer;

                float bufferLength = deviceInfo.DefaultUpdatePeriod > 0
                    ? (float) deviceInfo.DefaultUpdatePeriod
                    : DEFAULT_BUFFER_LENGTH_SECONDS;

                if (!BassWasapi.InitEx(_wasapiDeviceIndex, sampleRate, channelCount, flags, bufferLength, 0,
                        BassWasapi.WasapiProc_Bass, (IntPtr) OutputMixerHandle))
                {
                    YargLogger.LogFormatError("Failed to initialize WASAPI device [{0}]: {1}",
                        Name, Bass.LastError);
                    FreeOutputGraph();
                    return false;
                }

                BassWasapi.CurrentDevice = _wasapiDeviceIndex;

                if (BassWasapi.GetInfo(out var wasapiInfo))
                {
                    YargLogger.LogFormatInfo(
                        "WASAPI Exclusive output initialized: {0} Hz, {1} ch, buffer {2} bytes ({3} format)",
                        wasapiInfo.Frequency, wasapiInfo.Channels, wasapiInfo.BufferLength, wasapiInfo.Format);
                }

                if (!BassWasapi.Start())
                {
                    YargLogger.LogFormatError("Failed to start WASAPI output: {0}", Bass.LastError);
                    BassWasapi.Free();
                    FreeOutputGraph();
                    return false;
                }

                _isStarted = true;
                SetVolume(_volume);
                BassWasapi.SetNotify(_notifyProcedure, IntPtr.Zero);
                return true;
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to start WASAPI Exclusive output");
                return false;
            }
        }

        public override OutputBufferInfo? GetBufferInfo()
        {
            if (!_isStarted)
            {
                return null;
            }

            try
            {
                BassWasapi.CurrentDevice = _wasapiDeviceIndex;
                if (!BassWasapi.GetInfo(out var info))
                {
                    return null;
                }

                int frames = (info.Channels > 0 && SampleRate > 0)
                    ? (info.BufferLength / (info.Channels * sizeof(float)))
                    : 0;

                return new OutputBufferInfo(Array.Empty<int>(), frames, info.Frequency, true);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to read WASAPI buffer info");
                return null;
            }
        }

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
            try
            {
                BassWasapi.CurrentDevice = _wasapiDeviceIndex;
                BassWasapi.SetNotify(null, IntPtr.Zero);
                BassWasapi.Stop(true);
                BassWasapi.Free();
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Error stopping WASAPI device");
            }

            FreeOutputGraph();
        }

        protected override void DisposeResources()
        {
            base.DisposeResources();
            _microphones.Dispose();
        }

        private void OnWasapiNotify(WasapiNotificationType notify, int device, IntPtr user)
        {
            if (device != _wasapiDeviceIndex && device != -1)
            {
                return;
            }

            if (notify is WasapiNotificationType.Fail or WasapiNotificationType.Disabled)
            {
                QueueRestart();
            }
        }

        private void QueueRestart()
        {
            if (Interlocked.Exchange(ref _restartQueued, 1) == 0)
            {
                UnityMainThreadCallback.QueueEvent(RestartOutput);
            }
        }

        private void RestartOutput()
        {
            Interlocked.Exchange(ref _restartQueued, 0);
            if (IsDisposed)
            {
                return;
            }

            YargLogger.LogInfo("Reinitializing WASAPI Exclusive output");
            RequestRestart();
        }

        private static int FindDevice(string rawName)
        {
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsEnabled && !info.IsInput && !info.IsLoopback &&
                        string.Equals(info.Name, rawName, StringComparison.Ordinal))
                    {
                        return i;
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to search WASAPI devices");
            }

            return -1;
        }
    }
}
