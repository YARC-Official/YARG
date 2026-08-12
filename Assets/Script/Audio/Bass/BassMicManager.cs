#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ManagedBass;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS
{
    // Manages all recording devices and the mics opened on them. Each physical device
    // is captured once and shared by every mic on it, so multi-input devices (e.g. a
    // USB audio interface with several inputs) only open the device a single time.
    // The manager probes devices to discover their real channel count, caches the
    // results, tracks which channels are already claimed, and handles
    // RecordingSession instances as mics are added and removed. BASS recording
    // calls act on the current recording device; _lock serializes all of them.
    internal sealed class BassMicManager
    {
        private readonly List<ActiveMic>         _activeMics   = new();
        private readonly Dictionary<string, int> _channelCache = new(StringComparer.Ordinal);
        private readonly object                  _lock         = new();
        private          List<string>            _deviceNames  = new();

        /// <summary>
        ///     Opens a mic on a physical device and claims its capture channel. All mics on
        ///     the same device share one <see cref="RecordingSession" />. Returns null if the
        ///     channel is already claimed or the device can't be opened.
        /// </summary>
        public MicDevice? CreateMic(InputDeviceInfo device)
        {
            if (device.DeviceId < 0)
            {
                int resolved = FindDeviceIndexByName(device.Name);
                if (resolved < 0)
                {
                    return null;
                }

                device = new InputDeviceInfo(resolved, device.Name, device.Channel, device.ChannelCount);
            }

            int captureChannels = GetChannelCount(device.DeviceId, device.Name);
            if (device.Channel >= captureChannels)
            {
                return null;
            }

            lock (_lock)
            {
                if (IsChannelClaimed(device.DeviceId, device.Channel))
                {
                    return null;
                }

                var session = GetOrCreateSession(device.DeviceId, device.Name, captureChannels);
                if (session == null)
                {
                    return null;
                }

                var mic = BassMicDevice.Create(device.DeviceId, device.Name, session, device.Channel);
                if (mic == null)
                {
                    if (FindActive(device.DeviceId) == null)
                    {
                        session.Dispose();
                        FreeDevice(device.DeviceId);
                    }

                    return null;
                }

                var entry = new ActiveMic(device.DeviceId, session);
                _activeMics.Add(entry);
                mic.Disposed += () => ReleaseMic(entry);
                mic.SetMonitoringLevel(SettingsManager.Settings.VocalMonitoring.Value);
                return mic;
            }
        }

        public UniTask<List<InputDeviceInfo>> GetAllDevicesAsync(CancellationToken cancellationToken = default)
        {
            return UniTask.RunOnThreadPool(() => GetAllDevices(), cancellationToken: cancellationToken);
        }

        /// <summary>
        ///     Returns every usable input device and the unclaimed channels on each
        /// </summary>
        public List<InputDeviceInfo> GetAllDevices()
        {
            var usable = GetDevices()
                .Where(d => IsUsable(d.Info))
                .ToList();

            RefreshCache(usable);

            ProbeChannels(usable);

            var result = new List<InputDeviceInfo>();
            foreach (var device in usable)
            {
                result.AddRange(GetUnclaimedInputs(device.Id, device.Info.Name));
            }

            return result;
        }

        private void RefreshCache(List<DeviceEntry> devices)
        {
            var names = new List<string>(devices.Count);
            foreach (var device in devices)
            {
                names.Add(device.Info.Name);
            }

            lock (_lock)
            {
                if (!names.SequenceEqual(_deviceNames))
                {
                    _channelCache.Clear();
                    _deviceNames = names;
                }
            }
        }

        private static List<DeviceEntry> GetDevices()
        {
            var devices = new List<DeviceEntry>();
            for (int i = 0; Bass.RecordGetDeviceInfo(i, out var info); i++)
            {
                devices.Add(new DeviceEntry(i, info));
            }

            return devices;
        }

        private static bool IsUsable(DeviceInfo info)
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

        private static int FindDeviceIndexByName(string name)
        {
            foreach (var device in GetDevices())
            {
                if (IsUsable(device.Info) && device.Info.Name == name)
                {
                    return device.Id;
                }
            }

            return -1;
        }

        private List<InputDeviceInfo> GetUnclaimedInputs(int deviceId, string name)
        {
            int channels = GetChannelCount(deviceId, name);
            var list = new List<InputDeviceInfo>(channels);

            for (int ch = 0; ch < channels; ch++)
            {
                if (IsChannelClaimed(deviceId, ch))
                {
                    continue;
                }

                list.Add(new InputDeviceInfo(deviceId, name, ch, channels));
            }

            return list;
        }

        private int GetChannelCount(int deviceId, string name)
        {
            lock (_lock)
            {
                var active = FindActive(deviceId);
                if (active != null)
                {
                    return active.Session.Channels;
                }

                if (_channelCache.TryGetValue(name, out int cached))
                {
                    return cached;
                }
            }

            int? channels;
            lock (_lock)
            {
                channels = ChannelProbe.Probe(deviceId, name);
                if (channels == null)
                {
                    _channelCache[name] = 1;
                    return 1;
                }

                _channelCache[name] = channels.Value;
            }

            return channels.Value;
        }

        private void ProbeChannels(List<DeviceEntry> devices)
        {
            foreach (var device in devices)
            {
                GetChannelCount(device.Id, device.Info.Name);
            }
        }

        private bool IsChannelClaimed(int deviceId, int channel)
        {
            lock (_lock)
            {
                return FindActive(deviceId)?.Session.IsChannelClaimed(channel) ?? false;
            }
        }

        private RecordingSession? GetOrCreateSession(int deviceId, string name, int channels)
        {
            var active = FindActive(deviceId);
            if (active != null)
            {
                return active.Session;
            }

            if (!Bass.RecordInit(deviceId) && Bass.LastError != Errors.Already)
            {
                YargLogger.LogFormatError("Failed to init recording device [{0}] '{1}': {2}", deviceId, name,
                    Bass.LastError);
                return null;
            }

            Bass.CurrentRecordingDevice = deviceId;
            var session = RecordingSession.Create(deviceId, channels);
            if (session == null)
            {
                FreeDevice(deviceId);
                return null;
            }

            return session;
        }

        private void ReleaseMic(ActiveMic mic)
        {
            lock (_lock)
            {
                _activeMics.Remove(mic);
                if (FindActive(mic.DeviceId) != null)
                {
                    return;
                }

                mic.Session.Dispose();
                FreeDevice(mic.DeviceId);
            }
        }

        private static void FreeDevice(int deviceId)
        {
            if (!Bass.RecordGetDeviceInfo(deviceId, out var info) || !info.IsInitialized)
            {
                return;
            }

            Bass.CurrentRecordingDevice = deviceId;
            if (!Bass.RecordFree())
            {
                YargLogger.LogFormatWarning("Failed to free recording device [{0}]: {1}", deviceId, Bass.LastError);
            }
        }

        private ActiveMic? FindActive(int deviceId) => _activeMics.FirstOrDefault(m => m.DeviceId == deviceId);

        private readonly struct DeviceEntry
        {
            public readonly int        Id;
            public readonly DeviceInfo Info;

            public DeviceEntry(int id, DeviceInfo info)
            {
                Id = id;
                Info = info;
            }
        }

        private sealed class ActiveMic
        {
            public ActiveMic(int deviceId, RecordingSession session)
            {
                DeviceId = deviceId;
                Session = session;
            }

            public int              DeviceId { get; }
            public RecordingSession Session  { get; }
        }

        private sealed class ChannelProbe : IDisposable
        {
            private const int TIMEOUT_MS = 400;

            private static readonly (int Channels, int Rate)[] PROBE_CONFIGS =
            {
                (8, 48000),
                (8, 44100),
                (2, 48000),
                (2, 44100),
                (1, 48000),
                (1, 44100),
            };

            private readonly ManualResetEventSlim _gotFrame = new(false);
            private readonly int                  _reportedChannels;
            private          short[]              _frame = Array.Empty<short>();

            private ChannelProbe(int reportedChannels)
            {
                _reportedChannels = reportedChannels;
            }

            public void Dispose() => _gotFrame.Dispose();

            public static int? Probe(int deviceId, string name)
            {
                bool initialized = Bass.RecordInit(deviceId);
                if (!initialized && Bass.LastError != Errors.Already)
                {
                    return null;
                }

                Bass.CurrentRecordingDevice = deviceId;
                int devicePeriod = Bass.GetConfig(Configuration.DevicePeriod);
                try
                {
                    foreach ((int channels, int rate) in PROBE_CONFIGS)
                    {
                        var probe = new ChannelProbe(channels);
                        int handle = Bass.RecordStart(rate, channels, BassFlags.Default,
                            devicePeriod, probe.Callback, IntPtr.Zero);

                        if (handle == 0)
                        {
                            probe.Dispose();
                            continue;
                        }

                        int channelCount;
                        try
                        {
                            channelCount = probe.CountChannels();
                        }
                        finally
                        {
                            Bass.ChannelStop(handle);
                            probe.Dispose();
                        }

                        if (channelCount == 0)
                        {
                            continue;
                        }

                        if (channels == 8 && channelCount < 3)
                        {
                            continue;
                        }

                        return channelCount;
                    }
                }
                finally
                {
                    if (initialized)
                    {
                        Bass.RecordFree();
                    }
                }

                YargLogger.LogTrace($"Channel probe: no usable frame from [{deviceId}] '{name}'");
                return null;
            }

            private int CountChannels()
            {
                int deadline = Environment.TickCount + TIMEOUT_MS;
                while (true)
                {
                    int remaining = deadline - Environment.TickCount;
                    if (remaining <= 0)
                    {
                        return 0;
                    }

                    bool received = _gotFrame.Wait(remaining);
                    if (!received)
                    {
                        return 0;
                    }

                    _gotFrame.Reset();

                    if (_frame.Length == 0 || IsFrameSilent(_frame))
                    {
                        continue;
                    }

                    int frameCount = _frame.Length / _reportedChannels;
                    if (frameCount == 0)
                    {
                        return 0;
                    }

                    short[][] deinterleaved = Deinterleave(_frame, _reportedChannels, frameCount);

                    int lastActive = -1;
                    for (int ch = 0; ch < _reportedChannels; ch++)
                    {
                        if (IsSilent(deinterleaved[ch]))
                        {
                            continue;
                        }

                        if (IsDuplicate(deinterleaved, ch))
                        {
                            continue;
                        }

                        lastActive = ch;
                    }

                    if (lastActive < 0)
                    {
                        return 0;
                    }

                    return lastActive + 1;
                }
            }

            private bool Callback(int handle, IntPtr buffer, int length, IntPtr user)
            {
                if (length <= 0)
                {
                    return true;
                }

                unsafe
                {
                    var span = new Span<short>((short*) buffer, length / sizeof(short));
                    _frame = span.ToArray();
                    _gotFrame.Set();
                }

                return true;
            }

            private static short[][] Deinterleave(short[] interleaved, int channels, int frameCount)
            {
                short[][] outBufs = new short[channels][];
                for (int ch = 0; ch < channels; ch++)
                {
                    short[] buf = new short[frameCount];
                    for (int i = 0; i < frameCount; i++)
                    {
                        buf[i] = interleaved[i * channels + ch];
                    }

                    outBufs[ch] = buf;
                }

                return outBufs;
            }

            private static bool IsSilent(short[] samples) => Array.TrueForAll(samples, s => s == 0);

            private static bool IsFrameSilent(short[] samples) => Array.TrueForAll(samples, s => s == 0);

            private static bool IsDuplicate(short[][] bufs, int channel)
            {
                for (int i = 0; i < channel; i++)
                {
                    if (ChannelsEquivalent(bufs[channel], bufs[i]))
                    {
                        return true;
                    }
                }

                return false;
            }

            /// <summary>
            ///     Checks whether two channels carry the same signal, tolerating a few
            ///     glitched samples. Mono devices are upmixed to identical channels,
            ///     but USB capture can corrupt a handful of samples per stream.
            /// </summary>
            private static bool ChannelsEquivalent(short[] a, short[] b)
            {
                int maxDiffering = a.Length / 100;
                int differing = 0;
                for (int i = 0; i < a.Length; i++)
                {
                    if (a[i] != b[i] && ++differing > maxDiffering)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}

