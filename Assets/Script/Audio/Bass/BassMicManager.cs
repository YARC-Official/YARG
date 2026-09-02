#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS
{
    internal sealed class BassMicManager
    {
        private readonly Dictionary<int, BassMicrophoneCapture> _captures             = new();
        private readonly Dictionary<(int Id, string Name), int> _channelCountByDevice = new();
        private readonly object                                 _lock                 = new();
        private readonly BassAudioRouter                        _router;

        public BassMicManager(BassAudioRouter router)
        {
            _router = router;
        }

        public MicDevice? CreateMic(InputDeviceInfo requestedDevice)
        {
            if (!TryResolveRequestedDevice(requestedDevice, out var device))
            {
                return null;
            }

            int channelCount = GetChannelCount(device.DeviceId, device.Name);

            if (!IsChannelInRange(device, channelCount))
            {
                return null;
            }

            return TryCreateClaimedMic(device, channelCount);
        }

        public List<InputDeviceInfo> GetAllDevices()
        {
            var devices = FindUsableDevices();

            RemoveMissingDevices(devices);
            WarmChannelCounts(devices);
            SnapshotState(out var channelCounts, out var claimedChannels);

            return BuildAvailableInputs(devices, channelCounts, claimedChannels);
        }

        private MicDevice? TryCreateClaimedMic(InputDeviceInfo device, int channelCount)
        {
            BassMicrophoneCapture? capture;

            lock (_lock)
            {
                capture = FindOrCreateCapture(device.DeviceId, channelCount);
                if (capture == null)
                {
                    return null;
                }

                if (!capture.TryClaimChannel(device.Channel))
                {
                    YargLogger.LogFormatWarning("Mic '{0}' channel {1} is already claimed", device.Name,
                        device.Channel);
                    return null;
                }
            }

            string displayName = channelCount > 1 ? $"{device.Name} - Channel {device.Channel + 1}" : device.Name;
            BassSharedMicSource? source = null;
            try
            {
                source = new BassSharedMicSource(capture, device.Name, displayName, device.Channel, _router,
                    () => ReleaseMic(device.DeviceId, device.Channel, capture));
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Failed to create mic '{displayName}'");
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

        private BassMicrophoneCapture? FindOrCreateCapture(int deviceId, int channels)
        {
            if (_captures.TryGetValue(deviceId, out var existing))
            {
                return existing;
            }

            var capture = BassMicrophoneCapture.Create(deviceId, channels);
            if (capture != null)
            {
                _captures.Add(deviceId, capture);
            }

            return capture;
        }

        private void ReleaseMic(int deviceId, int channel, BassMicrophoneCapture capture)
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

        private void RemoveMissingDevices(List<(int Id, DeviceInfo Info)> devices)
        {
            var present = new HashSet<(int Id, string Name)>();

            foreach (var device in devices)
            {
                present.Add((device.Id, device.Info.Name));
            }

            lock (_lock)
            {
                var keys = new List<(int Id, string Name)>(_channelCountByDevice.Keys);

                foreach (var key in keys)
                {
                    if (!present.Contains(key))
                    {
                        _channelCountByDevice.Remove(key);
                    }
                }
            }
        }

        private int GetChannelCount(int deviceId, string name)
        {
            var key = (deviceId, name);

            lock (_lock)
            {
                if (_captures.TryGetValue(deviceId, out var activeGraph))
                {
                    return activeGraph.Channels;
                }

                if (_channelCountByDevice.TryGetValue(key, out int cached))
                {
                    return cached;
                }

                return BassMicrophoneCapture.WithSystemLock(() =>
                {
                    int detected = BassMicChannelProbe.DetectChannelCount(deviceId, name) ?? 1;
                    _channelCountByDevice[key] = detected;
                    return detected;
                });
            }
        }

        private void WarmChannelCounts(List<(int Id, DeviceInfo Info)> devices)
        {
            foreach (var device in devices)
            {
                GetChannelCount(device.Id, device.Info.Name);
            }
        }

        private void SnapshotState(out Dictionary<(int Id, string Name), int> channelCounts,
            out HashSet<(int DeviceId, int Channel)> claimedChannels)
        {
            lock (_lock)
            {
                channelCounts = new Dictionary<(int Id, string Name), int>(_channelCountByDevice);
                claimedChannels = new HashSet<(int DeviceId, int Channel)>();

                foreach (var entry in _captures)
                {
                    for (int channel = 0; channel < entry.Value.Channels; channel++)
                    {
                        if (entry.Value.IsChannelClaimed(channel))
                        {
                            claimedChannels.Add((entry.Key, channel));
                        }
                    }
                }
            }
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

            resolved = new InputDeviceInfo(foundId, requested.Name, requested.Channel, requested.ChannelCount);
            return true;
        }

        private static bool IsChannelInRange(InputDeviceInfo device, int channelCount)
        {
            if (device.Channel >= 0 && device.Channel < channelCount)
            {
                return true;
            }

            YargLogger.LogFormatWarning("Mic '{0}' channel {1} is out of range (device has {2} channels)", device.Name,
                device.Channel, channelCount);
            return false;
        }

        private static List<(int Id, DeviceInfo Info)> FindUsableDevices()
        {
            var list = new List<(int Id, DeviceInfo Info)>();

            for (int i = 0; Bass.RecordGetDeviceInfo(i, out var info); i++)
            {
                if (IsUsableDevice(info))
                {
                    list.Add((i, info));
                }
            }

            return list;
        }

        private static bool IsUsableDevice(DeviceInfo info)
        {
            if (!info.IsEnabled || info.IsLoopback)
            {
                return false;
            }

            if (info.Name == "Default")
            {
                return false;
            }

            if (info.Name.StartsWith("Loopback", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private static int FindDeviceIdByName(string name)
        {
            foreach (var device in FindUsableDevices())
            {
                if (device.Info.Name == name)
                {
                    return device.Id;
                }
            }

            return -1;
        }

        private static List<InputDeviceInfo> BuildAvailableInputs(List<(int Id, DeviceInfo Info)> devices,
            Dictionary<(int Id, string Name), int> channelCounts, HashSet<(int DeviceId, int Channel)> claimedChannels)
        {
            var available = new List<InputDeviceInfo>();

            foreach (var device in devices)
            {
                if (!channelCounts.TryGetValue((device.Id, device.Info.Name), out int channels))
                {
                    continue;
                }

                for (int channel = 0; channel < channels; channel++)
                {
                    if (claimedChannels.Contains((device.Id, channel)))
                    {
                        continue;
                    }

                    available.Add(new InputDeviceInfo(device.Id, device.Info.Name, channel, channels));
                }
            }

            return available;
        }
    }
}
