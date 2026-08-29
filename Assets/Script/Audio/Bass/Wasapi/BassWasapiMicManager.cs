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
    ///     Tracks and manages all active WASAPI microphone captures, coordinating channel claims,
    ///     device notification dispatching, and microphone lifecycle.
    /// </summary>
    internal sealed class BassWasapiMicManager : IDisposable
    {
        private readonly Dictionary<int, BassWasapiMicCapture> _captures = new();
        private readonly WasapiNotifyProcedure                 _notifyProcedure;
        private readonly BassAudioRouter                       _router;
        private          bool                                  _notificationsRegistered;
        private          BassWasapiOutput?                     _output;

        public BassWasapiMicManager(BassAudioRouter router)
        {
            _router = router;
            _notifyProcedure = OnWasapiNotify;
        }

        public void Dispose()
        {
            _output = null;
            UnregisterNotifications();

            foreach (var capture in _captures.Values)
            {
                capture.Dispose();
            }

            _captures.Clear();
        }

        public bool AttachOutput(BassWasapiOutput output)
        {
            _output = output;
            if (BassWasapi.SetNotify(_notifyProcedure, IntPtr.Zero))
            {
                _notificationsRegistered = true;
                return true;
            }

            _output = null;
            YargLogger.LogFormatError("Failed to register WASAPI device notifications: {0}", Bass.LastError);
            return false;
        }

        public void DetachOutput(BassWasapiOutput output)
        {
            if (!ReferenceEquals(_output, output))
            {
                return;
            }

            _output = null;
            UnregisterNotificationsIfUnused();
        }

        public List<InputDeviceInfo> GetAllDevices()
        {
            var result = new List<InputDeviceInfo>();
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (!info.IsUsableInput())
                    {
                        continue;
                    }

                    string baseName = info.GetInputDisplayName();
                    int channelCount = info.GetChannelCount();

                    for (int channel = 0; channel < channelCount; channel++)
                    {
                        if (_captures.TryGetValue(i, out var capture) && capture.IsChannelClaimed(channel))
                        {
                            continue;
                        }

                        result.Add(new InputDeviceInfo(i, baseName, channel, channelCount));
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
            if (!TryResolveRequestedDevice(requested, out var device))
            {
                return null;
            }

            var capture = FindOrCreateCapture(device.DeviceId, device.ChannelCount);
            if (capture == null)
            {
                return null;
            }

            if (!capture.TryClaimChannel(device.Channel))
            {
                YargLogger.LogFormatWarning("WASAPI mic '{0}' channel {1} is already claimed",
                    device.Name, device.Channel);
                return null;
            }

            var source = BassWasapiMicSource.Create(capture, device, _router,
                () => ReleaseMic(device.DeviceId, device.Channel, capture));
            if (source == null)
            {
                ReleaseMic(device.DeviceId, device.Channel, capture);
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

        private bool TryResolveRequestedDevice(InputDeviceInfo requested, out InputDeviceInfo resolved)
        {
            try
            {
                if (requested.DeviceId >= 0 &&
                    BassWasapi.GetDeviceInfo(requested.DeviceId, out var info) &&
                    info.MatchesRequested(requested.DeviceId, requested, out resolved))
                {
                    return true;
                }

                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var devInfo); i++)
                {
                    if (devInfo.MatchesRequested(i, requested, out resolved))
                    {
                        return true;
                    }
                }
            }
            catch (Exception exception)
            {
                YargLogger.LogException(exception, $"Failed to resolve WASAPI mic '{requested.Name}'");
            }

            resolved = default;
            return false;
        }

        private BassWasapiMicCapture? FindOrCreateCapture(int deviceId, int channels)
        {
            if (_captures.TryGetValue(deviceId, out var existing))
            {
                return existing;
            }

            var capture = BassWasapiMicCapture.Create(deviceId, channels);
            if (capture != null)
            {
                _captures.Add(deviceId, capture);
            }

            return capture;
        }

        private void OnWasapiNotify(WasapiNotificationType notify, int device, IntPtr user)
        {
            _output?.OnWasapiNotify(notify, device);
            foreach (var capture in _captures.Values)
            {
                capture.OnWasapiNotify(notify, device);
            }
        }

        private void ReleaseMic(int deviceId, int channel, BassWasapiMicCapture capture)
        {
            if (_captures.TryGetValue(deviceId, out var current) && ReferenceEquals(current, capture))
            {
                capture.ReleaseChannel(channel);
                if (!capture.HasClaimedChannel)
                {
                    _captures.Remove(deviceId);
                    capture.Dispose();
                    UnregisterNotificationsIfUnused();
                }
            }
        }

        private void UnregisterNotificationsIfUnused()
        {
            if (_output == null && _captures.Count == 0)
            {
                UnregisterNotifications();
            }
        }

        private void UnregisterNotifications()
        {
            if (_notificationsRegistered && BassWasapi.SetNotify(null, IntPtr.Zero))
            {
                _notificationsRegistered = false;
            }
        }
    }

    internal static class BassWasapiMicManagerExtensions
    {
        public static bool IsUsableInput(this WasapiDeviceInfo info) =>
            info.IsEnabled && info.IsInput && !info.IsLoopback;

        public static string GetInputDisplayName(this WasapiDeviceInfo info) =>
            $"{BassWasapiOutput.DEVICE_PREFIX}{info.Name}";

        public static int GetChannelCount(this WasapiDeviceInfo info) => Math.Max(1, info.MixChannels);

        public static bool MatchesRequested(this WasapiDeviceInfo info, int deviceId, InputDeviceInfo requested,
            out InputDeviceInfo resolved)
        {
            if (info.IsUsableInput())
            {
                string displayName = info.GetInputDisplayName();
                int channelCount = info.GetChannelCount();
                bool nameMatches = string.IsNullOrEmpty(requested.Name) ||
                    string.Equals(displayName, requested.Name, StringComparison.OrdinalIgnoreCase);

                if (nameMatches && requested.Channel.IsValidChannel(channelCount))
                {
                    resolved = new InputDeviceInfo(deviceId, displayName, requested.Channel, channelCount);
                    return true;
                }
            }

            resolved = default;
            return false;
        }
    }
}