#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            var timing = Stopwatch.StartNew();
            if (!TryResolveRequestedDevice(requestedDevice, out var device))
            {
                return null;
            }

            long resolveMs = timing.ElapsedMilliseconds;
            int channelCount = GetChannelCount(device.DeviceId, device.Name);
            long probeMs = timing.ElapsedMilliseconds - resolveMs;

            if (!IsChannelInRange(device, channelCount))
            {
                return null;
            }

            var microphone = TryCreateClaimedMic(device, channelCount);
            if (microphone != null)
                YargLogger.LogFormatInfo("Microphone opened [{0}] '{1}' channel {2}: resolve {3} ms, probe {4} ms, setup {5} ms",
                    device.DeviceId, device.Name, device.Channel, resolveMs, probeMs,
                    timing.ElapsedMilliseconds - resolveMs - probeMs);
            return microphone;
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
            var timing = Stopwatch.StartNew();

            lock (_lock)
            {
                capture = FindOrCreateCapture(device.DeviceId, channelCount);
                if (capture == null)
                {
                    return null;
                }

                if (!IsChannelInRange(device, capture.Channels))
                {
                    ReleaseMic(device.DeviceId, device.Channel, capture);
                    return null;
                }

                if (!capture.TryClaimChannel(device.Channel))
                {
                    YargLogger.LogFormatWarning("Mic '{0}' channel {1} is already claimed", device.Name,
                        device.Channel);
                    return null;
                }
            }

            long captureMs = timing.ElapsedMilliseconds;
            // Keep capture alive until the monitor takes ownership. Native startup can block;
            // finish it before the playing output mixer can pull from this recording stream.
            capture.AddListener();
            try
            {
                if (!capture.Start())
                {
                    ReleaseMic(device.DeviceId, device.Channel, capture);
                    return null;
                }
                long startMs = timing.ElapsedMilliseconds - captureMs;

                string displayName = channelCount > 1 ? $"{device.Name} - Channel {device.Channel + 1}" : device.Name;
                BassSharedMicSource? source;
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

                long signalMs = timing.ElapsedMilliseconds - captureMs - startMs;
                if (!source.IsValid)
                {
                    source.Dispose();
                    return null;
                }

                var mic = BassMicDevice.Create(source);
                if (mic == null)
                {
                    source.Dispose();
                }

                if (mic != null)
                    YargLogger.LogFormatInfo("Microphone setup [{0}]: capture {1} ms, start {2} ms, signal/monitor {3} ms, analyzer {4} ms",
                        device.DeviceId, captureMs, startMs, signalMs,
                        timing.ElapsedMilliseconds - captureMs - startMs - signalMs);
                return mic;
            }
            finally
            {
                capture.RemoveListener();
            }
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
#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
                    int detected = 2;
#else
                    int detected = BassMicChannelProbe.DetectChannelCount(deviceId, name) ?? 1;
#endif
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

        private bool TryResolveRequestedDevice(InputDeviceInfo requested, out InputDeviceInfo resolved)
        {
            if (requested.DeviceId >= 0)
            {
                resolved = requested;
                return true;
            }

            int foundId = FindDeviceIdByName(requested.Name, requested.Channel);
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

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
            if (!IsUsableLinuxDevice(info))
            {
                return false;
            }
#else
            if (info.Name == "Default")
            {
                return false;
            }
#endif

            if (info.Name.StartsWith("Loopback", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

#if UNITY_EDITOR_LINUX || UNITY_STANDALONE_LINUX
        private static bool IsLinuxSoundServerDevice(DeviceInfo info)
        {
            if (info.IsDefault || info.Name.Equals("Default", StringComparison.OrdinalIgnoreCase) ||
                info.Name.Equals("Default Audio Device", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string driver = info.Driver ?? string.Empty;
            return IsAlsaDevice(driver, "default") || IsAlsaDevice(driver, "pulse") ||
                IsAlsaDevice(driver, "pipewire");
        }

        private static bool IsUsableLinuxDevice(DeviceInfo info)
        {
            if (IsLinuxSoundServerDevice(info))
            {
                return true;
            }

            string driver = info.Driver ?? string.Empty;
            return IsAlsaDevice(driver, "hw") || IsAlsaDevice(driver, "plughw");
        }

        private static bool IsAlsaDevice(string driver, string device) =>
            driver.Equals(device, StringComparison.OrdinalIgnoreCase) ||
            driver.StartsWith(device + ":", StringComparison.OrdinalIgnoreCase);
#endif

        private int FindDeviceIdByName(string name, int channel)
        {
            foreach (var device in FindUsableDevices())
            {
                if (device.Info.Name != name) continue;
                lock (_lock)
                {
                    // Identical microphones share a saved name. Each binding needs its own channel claim.
                    if (_captures.TryGetValue(device.Id, out var capture) && capture.IsChannelClaimed(channel))
                        continue;
                }
                if (channel >= 0 && channel < GetChannelCount(device.Id, name))
                    return device.Id;
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
