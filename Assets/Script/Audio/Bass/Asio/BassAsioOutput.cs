#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using ManagedBass.Asio;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Handles low-latency audio playback and microphone routing through an ASIO driver. All audio
    ///     is combined into a single decode mixer that the ASIO hardware callback pulls from directly.
    /// </summary>
    internal sealed class BassAsioOutput : BassOutput
    {
        internal const string DEVICE_PREFIX   = "ASIO: ";
        private const  int    NO_DEVICE_ID    = -1;

        private readonly int                 _asioDeviceIndex;
        private readonly List<BassAsioInput> _inputs = new();
        private readonly BassAsioMics        _microphones;
        private readonly BassAudioRouter     _router;
        private          BassAsioDriver      _driver;
        private          int                 _latencyFrames;
        private          double              _volume = 1;

        private BassAsioOutput(string name, BassOutputDevice device, int asioDeviceIndex, BassAudioRouter router,
            BassAsioMics microphones) : base(name, device)
        {
            _asioDeviceIndex = asioDeviceIndex;
            _router = router;
            _microphones = microphones;
            _driver = new BassAsioDriver(asioDeviceIndex, RequestRestart);
        }

        public override int HeardLatencyMilliseconds =>
            SampleRate > 0 ? (int) Math.Round(_latencyFrames * 1000.0 / SampleRate) : 0;

        internal override int EndpointDelayFrames => _latencyFrames;

        internal override double SongPlaybackStartDelay => SampleRate > 0 ? _latencyFrames / (double) SampleRate : 0;

        internal override bool UsesIndependentClock => true;

        internal string DriverId => _driver.DriverId;

        internal static bool IsAsioDevice(string name) =>
            name.StartsWith(DEVICE_PREFIX, StringComparison.Ordinal);

        public static BassAsioOutput? Find(string name, BassAudioRouter router, BassAsioMics microphones)
        {
            if (!IsAsioDevice(name))
            {
                return null;
            }

            int deviceIndex = FindDriver(name.Substring(DEVICE_PREFIX.Length));
            if (deviceIndex < 0)
            {
                return null;
            }

            var device = BassOutputDevice.CreateAsio(name);
            return device == null ? null : new BassAsioOutput(name, device, deviceIndex, router, microphones);
        }

        public static List<(int id, string name)> GetDevices()
        {
            var devices = new List<(int id, string name)>();
            try
            {
                for (int deviceIndex = 0; deviceIndex < BassAsio.DeviceCount; deviceIndex++)
                {
                    devices.Add((deviceIndex, DEVICE_PREFIX + BassAsio.GetDeviceInfo(deviceIndex).Name));
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to enumerate ASIO devices");
            }

            return devices;
        }

        public override bool Start()
        {
            try
            {
                Device.Use();
                if (_driver.IsDisposed)
                {
                    _driver = new BassAsioDriver(_asioDeviceIndex, RequestRestart);
                }

                if (!_driver.Initialize())
                {
                    return false;
                }

                if (!CreateOutput())
                {
                    return false;
                }

                if (!_driver.Start())
                {
                    return false;
                }

                _driver.RegisterNotify();
                _latencyFrames = _driver.GetLatencyFrames();
                _microphones.Attach(this);
                return true;
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to start ASIO output");
                return false;
            }
        }

        public override OutputBufferInfo? GetBufferInfo()
        {
            try
            {
                BassAsio.CurrentDevice = _asioDeviceIndex;
                var info = BassAsio.Info;
                return new OutputBufferInfo(Array.Empty<int>(), info.PreferredBufferLength, _driver.SampleRate,
                    isDriverControlled: true);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to read ASIO buffer sizes");
                return null;
            }
        }

        public override bool OpenControlPanel()
        {
            try
            {
                BassAsio.CurrentDevice = _asioDeviceIndex;
                if (BassAsio.ControlPanel())
                {
                    return true;
                }

                YargLogger.LogFormatError("Failed to open ASIO control panel: {0}", BassAsio.LastError);
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to open ASIO control panel");
            }

            return false;
        }

        public override IReadOnlyList<InputDeviceInfo> GetInputs()
        {
            var available = new List<InputDeviceInfo>();
            foreach (var input in _inputs)
            {
                if (!_microphones.IsClaimed(input.DriverId, input.ChannelIndex))
                {
                    available.Add(new InputDeviceInfo(NO_DEVICE_ID, $"{DEVICE_PREFIX}{input.DriverName}",
                        input.ChannelIndex, _inputs.Count));
                }
            }

            return available;
        }

        public override MicDevice? CreateInput(InputDeviceInfo requested)
        {
            if (requested.Channel < 0 || requested.Channel >= _inputs.Count)
            {
                return null;
            }

            var input = _inputs[requested.Channel];
            string name = $"{DEVICE_PREFIX}{input.DriverName}";
            if (!string.Equals(name, requested.Name, StringComparison.Ordinal))
            {
                return null;
            }

            var info = new InputDeviceInfo(NO_DEVICE_ID, name, input.ChannelIndex, _inputs.Count);
            return _microphones.Create(input, info);
        }

        public override void SetVolume(double volume)
        {
            _volume = volume;
            if (OutputMixerHandle != 0 && !_driver.SetOutputVolume(volume))
            {
                YargLogger.LogError("Failed to set ASIO output volume");
            }
        }

        internal BassAsioInput? ClaimInput(string driverId, int channelIndex)
        {
            if (IsDisposed || !_driver.IsStarted)
            {
                return null;
            }

            if (!string.Equals(driverId, _driver.DriverId, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            if (channelIndex < 0 || channelIndex >= _inputs.Count)
            {
                return null;
            }

            var input = _inputs[channelIndex];
            if (!input.Attach(_router) || !_driver.ActivateInput(input))
            {
                return null;
            }

            return input.Claim() ? input : null;
        }

        protected override void StopOutput()
        {
            _microphones.Detach(this);
            _driver.Stop();
            _driver.Dispose();
            DisposeOutput();
        }

        private bool CreateOutput()
        {
            if (!CreateOutputGraph(_driver.SampleRate, _driver.OutputCount, BassFlags.Decode, _driver.CallbackFrames))
            {
                return false;
            }

            for (int channel = 0; channel < _driver.InputCount; channel++)
            {
                var input = BassAsioInput.Create(_driver.DriverId, _driver.DriverName, channel, SampleRate,
                    _driver.CallbackFrames);
                if (input == null)
                {
                    YargLogger.LogFormatError("Failed to create ASIO input {0}: {1}", channel, Bass.LastError);
                    return false;
                }

                _inputs.Add(input);
            }

            if (!_driver.AttachOutput(OutputMixerHandle))
            {
                return false;
            }

            if (!_driver.SetOutputVolume(_volume))
            {
                YargLogger.LogError("Failed to set ASIO output volume");
                return false;
            }

            return true;
        }

        private void DisposeOutput()
        {
            foreach (var input in _inputs)
            {
                input.FreeNativeStreams();
            }

            _inputs.Clear();
            FreeOutputGraph();
        }

        private static int FindDriver(string driverName)
        {
            try
            {
                for (int index = 0; index < BassAsio.DeviceCount; index++)
                {
                    if (BassAsio.GetDeviceInfo(index).Name == driverName)
                    {
                        return index;
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to find ASIO device");
            }

            return -1;
        }
    }
}
