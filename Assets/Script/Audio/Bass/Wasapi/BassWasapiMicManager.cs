#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass.Wasapi;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Wasapi
{
    /// <summary>
    ///     Discovers and manages WASAPI Exclusive recording devices and input channels.
    /// </summary>
    internal sealed class BassWasapiMicManager : IDisposable
    {
        private readonly Dictionary<int, BassWasapiMicrophoneCapture> _captures = new();
        private readonly object                                       _lock     = new();
        private readonly BassAudioRouter                              _router;

        public BassWasapiMicManager(BassAudioRouter router)
        {
            _router = router;
        }

        public MicDevice? CreateMic(InputDeviceInfo requestedDevice)
        {
            if (!TryResolveRequestedDevice(requestedDevice, out var device))
            {
                return null;
            }

            int channelCount = GetChannelCount(device.DeviceId);
            if (!IsChannelInRange(device, channelCount))
            {
                return null;
            }

            return TryCreateClaimedMic(device, channelCount);
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

        private MicDevice? TryCreateClaimedMic(InputDeviceInfo device, int channelCount)
        {
            BassWasapiMicrophoneCapture? capture;

            lock (_lock)
            {
                capture = FindOrCreateCapture(device.DeviceId, channelCount);
                if (capture == null)
                {
                    return null;
                }

                if (!capture.TryClaimChannel(device.Channel))
                {
                    YargLogger.LogFormatWarning("WASAPI Mic '{0}' channel {1} is already claimed",
                        device.Name, device.Channel);
                    return null;
                }
            }

            string displayName = channelCount > 1
                ? $"{device.Name} - Channel {device.Channel + 1}"
                : device.Name;

            BassWasapiMicSource? source = null;
            try
            {
                source = new BassWasapiMicSource(capture, device.Name, displayName, device.Channel, _router,
                    () => ReleaseMic(device.DeviceId, device.Channel, capture));
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Failed to create WASAPI mic '{displayName}'");
                ReleaseMic(device.DeviceId, device.Channel, capture);
                return null;
            }

            if (!source.IsValid || !capture.Start())
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

        private BassWasapiMicrophoneCapture? FindOrCreateCapture(int deviceId, int channels)
        {
            if (_captures.TryGetValue(deviceId, out var existing))
            {
                return existing;
            }

            var capture = BassWasapiMicrophoneCapture.Create(deviceId, channels);
            if (capture != null)
            {
                _captures.Add(deviceId, capture);
            }

            return capture;
        }

        private void ReleaseMic(int deviceId, int channel, BassWasapiMicrophoneCapture capture)
        {
            lock (_lock)
            {
                if (!_captures.TryGetValue(deviceId, out var current) || !ReferenceEquals(current, capture))
                {
                    return;
                }

                capture.ReleaseChannel(channel);

                if (capture.HasClaimedChannel)
                {
                    return;
                }

                _captures.Remove(deviceId);
                capture.Dispose();
            }
        }

        private static int GetChannelCount(int deviceId)
        {
            if (BassWasapi.GetDeviceInfo(deviceId, out var info) && info.MixChannels > 0)
            {
                return info.MixChannels;
            }

            return 1;
        }

        private static bool TryResolveRequestedDevice(InputDeviceInfo requested, out InputDeviceInfo resolved)
        {
            if (requested.DeviceId >= 0)
            {
                resolved = requested;
                return true;
            }

            int foundId = FindDeviceIdByName(requested.Name);
            if (foundId < 0)
            {
                resolved = default;
                return false;
            }

            int channelCount = GetChannelCount(foundId);
            resolved = new InputDeviceInfo(foundId, requested.Name, requested.Channel, channelCount);
            return true;
        }

        private static int FindDeviceIdByName(string name)
        {
            string searchName = name.StartsWith(BassWasapiOutput.DEVICE_PREFIX, StringComparison.Ordinal)
                ? name.Substring(BassWasapiOutput.DEVICE_PREFIX.Length)
                : name;

            for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
            {
                if (info.IsEnabled && info.IsInput && !info.IsLoopback &&
                    string.Equals(info.Name, searchName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool IsChannelInRange(InputDeviceInfo device, int channelCount)
        {
            if (device.Channel >= 0 && device.Channel < channelCount)
            {
                return true;
            }

            YargLogger.LogFormatWarning("WASAPI Mic '{0}' channel {1} is out of range (device has {2} channels)",
                device.Name, device.Channel, channelCount);
            return false;
        }
    }
}
