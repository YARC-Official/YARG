#nullable enable
using System;
using System.Collections.Generic;
using ManagedBass;
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
        private readonly WasapiNotifyProcedure                         _notifyProcedure;
        private          BassWasapiOutput?                             _output;
        private          bool                                           _notificationsRegistered;

        public BassWasapiMicManager(BassAudioRouter router)
        {
            _router = router;
            _notifyProcedure = OnWasapiNotify;
        }

        public bool AttachOutput(BassWasapiOutput output)
        {
            lock (_lock)
            {
                _output = output;
                if (BassWasapiMicrophoneCapture.SetNotification(_notifyProcedure))
                {
                    _notificationsRegistered = true;
                    return true;
                }

                _output = null;
                YargLogger.LogFormatError("Failed to register WASAPI device notifications: {0}", Bass.LastError);
                return false;
            }
        }

        public void DetachOutput(BassWasapiOutput output)
        {
            lock (_lock)
            {
                if (!ReferenceEquals(_output, output))
                {
                    return;
                }

                _output = null;
                if (_captures.Count == 0 && _notificationsRegistered)
                {
                    if (BassWasapiMicrophoneCapture.SetNotification(null))
                    {
                        _notificationsRegistered = false;
                    }
                }
            }
        }

        public List<InputDeviceInfo> GetAllDevices()
        {
            var result = new List<InputDeviceInfo>();
            try
            {
                for (int i = 0; BassWasapi.GetDeviceInfo(i, out var info); i++)
                {
                    if (info.IsUsableInput())
                    {
                        string baseName = $"{BassWasapiOutput.DEVICE_PREFIX}{info.Name}";
                        int channelCount = Math.Max(1, info.MixChannels);

                        for (int channel = 0; channel < channelCount; channel++)
                        {
                            lock (_lock)
                            {
                                if (_captures.TryGetValue(i, out var capture) && capture.IsChannelClaimed(channel))
                                {
                                    continue;
                                }
                            }

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
            InputDeviceInfo device = default;
            bool found = false;
            foreach (var available in GetAllDevices())
            {
                if (available.Matches(requested))
                {
                    device = available;
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return null;
            }

            BassWasapiMicrophoneCapture? capture;
            lock (_lock)
            {
                if (!_captures.TryGetValue(device.DeviceId, out capture))
                {
                    capture = BassWasapiMicrophoneCapture.Create(device.DeviceId, device.ChannelCount);
                    if (capture == null)
                    {
                        return null;
                    }

                    _captures.Add(device.DeviceId, capture);
                }

                if (!capture.TryClaimChannel(device.Channel))
                {
                    YargLogger.LogFormatWarning("WASAPI mic '{0}' channel {1} is already claimed",
                        device.Name, device.Channel);
                    return null;
                }
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

        public void Dispose()
        {
            lock (_lock)
            {
                _output = null;
                if (_notificationsRegistered)
                {
                    if (BassWasapiMicrophoneCapture.SetNotification(null))
                    {
                        _notificationsRegistered = false;
                    }
                }
                foreach (var capture in _captures.Values)
                {
                    capture.Dispose();
                }

                _captures.Clear();
            }
        }

        private void OnWasapiNotify(WasapiNotificationType notify, int device, IntPtr user)
        {
            BassWasapiOutput? output;
            BassWasapiMicrophoneCapture[] captures;
            lock (_lock)
            {
                output = _output;
                captures = new BassWasapiMicrophoneCapture[_captures.Count];
                _captures.Values.CopyTo(captures, 0);
            }

            output?.OnWasapiNotify(notify, device);
            foreach (var capture in captures)
            {
                capture.OnWasapiNotify(notify, device);
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
                        if (_output == null && _notificationsRegistered)
                        {
                            if (BassWasapiMicrophoneCapture.SetNotification(null))
                            {
                                _notificationsRegistered = false;
                            }
                        }
                    }
                }
            }
        }
    }

    internal static class BassWasapiMicManagerExtensions
    {
        public static bool IsUsableInput(this WasapiDeviceInfo info) =>
            info.IsEnabled && info.IsInput && !info.IsLoopback;

        public static bool Matches(this InputDeviceInfo available, InputDeviceInfo requested) =>
            (requested.DeviceId >= 0
                ? available.DeviceId == requested.DeviceId
                : string.Equals(available.Name, requested.Name, StringComparison.OrdinalIgnoreCase)) &&
            available.Channel == requested.Channel;
    }
}
