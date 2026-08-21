#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass.Wasapi;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Wasapi
{
    internal sealed class BassWasapiMicManager : IDisposable
    {
        private readonly Dictionary<int, BassWasapiMicrophoneCapture> _captures = new();
        private readonly object                                       _lock     = new();
        private readonly BassAudioRouter                              _router;

        public BassWasapiMicManager(BassAudioRouter router)
        {
            _router = router;
        }

        public List<InputDeviceInfo> GetAllDevices()
        {
            var result = new List<InputDeviceInfo>();
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsEnabled && info.IsInput && !info.IsLoopback)
                    {
                        string baseName = $"{BassWasapiOutput.DEVICE_PREFIX}{info.Name}";
                        int channelCount = Math.Max(1, info.MixChannels);

                        for (int channel = 0; channel < channelCount; channel++)
                        {
                            result.Add(new InputDeviceInfo(i, baseName, channel, channelCount));
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, "Failed to enumerate WASAPI input devices");
            }

            return result;
        }

        public MicDevice? CreateMic(InputDeviceInfo requested)
        {
            int deviceId = requested.DeviceId >= 0 ? requested.DeviceId : FindDeviceIdByName(requested.Name);
            if (deviceId < 0 || !BassWasapi.GetDeviceInfo(deviceId, out var info))
            {
                return null;
            }

            int channelCount = Math.Max(1, info.MixChannels);
            if (requested.Channel < 0 || requested.Channel >= channelCount)
            {
                YargLogger.LogFormatWarning("WASAPI mic '{0}' channel {1} is out of range (device has {2} channels)",
                    requested.Name, requested.Channel, channelCount);
                return null;
            }

            var device = new InputDeviceInfo(deviceId, requested.Name, requested.Channel, channelCount);

            BassWasapiMicrophoneCapture? capture;
            lock (_lock)
            {
                if (!_captures.TryGetValue(deviceId, out capture))
                {
                    capture = BassWasapiMicrophoneCapture.Create(deviceId, channelCount);
                    if (capture == null)
                    {
                        return null;
                    }

                    _captures.Add(deviceId, capture);
                }

                if (!capture.TryClaimChannel(device.Channel))
                {
                    YargLogger.LogFormatWarning("WASAPI mic '{0}' channel {1} is already claimed",
                        device.Name, device.Channel);
                    return null;
                }
            }

            var source = BassWasapiMicSource.Create(capture, device, _router,
                () => ReleaseMic(deviceId, device.Channel, capture));
            if (source == null)
            {
                ReleaseMic(deviceId, device.Channel, capture);
                return null;
            }

            if (!capture.Start())
            {
                source.Dispose();
                return null;
            }

            var mic = BassMicDevice.Create(source);
            if (mic == null)
            {
                source.Dispose();
            }

            return mic;
        }

        public void Dispose()
        {
            lock (_lock)
            {
                foreach (var capture in _captures.Values)
                {
                    capture.Dispose();
                }

                _captures.Clear();
            }
        }

        private void ReleaseMic(int deviceId, int channel, BassWasapiMicrophoneCapture capture)
        {
            lock (_lock)
            {
                if (_captures.TryGetValue(deviceId, out var current) && ReferenceEquals(current, capture))
                {
                    capture.ReleaseChannel(channel);
                    if (!capture.HasClaimedChannel)
                    {
                        _captures.Remove(deviceId);
                        capture.Dispose();
                    }
                }
            }
        }

        private int FindDeviceIdByName(string name)
        {
            foreach (var device in GetAllDevices())
            {
                if (string.Equals(device.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return device.DeviceId;
                }
            }

            return -1;
        }
    }
}
