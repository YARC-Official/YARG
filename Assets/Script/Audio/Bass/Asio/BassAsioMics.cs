#nullable enable
using System;
using System.Collections.Generic;
using YARG.Core.Audio;
using YARG.Core.Logging;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Tracks and manages all active ASIO microphone devices, ensuring their input streams remain connected
    ///     and properly re-initialized when the active ASIO output or driver restarts.
    /// </summary>
    internal sealed class BassAsioMics
    {
        private readonly List<BassAsioMicDevice> _devices = new();
        private          BassAsioOutput?         _output;

        public void Attach(BassAsioOutput output)
        {
            _output = output;
            foreach (var device in _devices)
            {
                Resume(device);
            }
        }

        public void Detach(BassAsioOutput output)
        {
            if (!ReferenceEquals(_output, output))
            {
                return;
            }

            foreach (var device in _devices)
            {
                device.Suspend();
            }

            _output = null;
        }

        public bool IsClaimed(string driverId, int channelIndex)
        {
            foreach (var device in _devices)
            {
                if (device.Matches(driverId, channelIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public BassAsioMicDevice? Create(BassAsioInput asioInput, InputDeviceInfo info)
        {
            if (IsClaimed(asioInput.DriverId, asioInput.ChannelIndex))
            {
                return null;
            }

            var claimedInput = _output?.ClaimInput(asioInput.DriverId, asioInput.ChannelIndex);
            if (claimedInput == null)
            {
                YargLogger.LogWarning($"Failed to acquire ASIO microphone '{info.DisplayName}'");
                return null;
            }

            var device = new BassAsioMicDevice(this, asioInput.DriverId, claimedInput, info);
            _devices.Add(device);
            return device;
        }

        internal void Release(BassAsioMicDevice device) => _devices.Remove(device);

        private void Resume(BassAsioMicDevice device)
        {
            var output = _output;
            if (output == null || !string.Equals(device.DriverId, output.DriverId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var input = output.ClaimInput(device.DriverId, device.ChannelIndex);
            if (input != null)
            {
                try
                {
                    device.Resume(input);
                }
                catch (Exception exception)
                {
                    YargLogger.LogException(exception, $"Failed to restore ASIO microphone '{device.DisplayName}'");
                }

                return;
            }

            YargLogger.LogWarning($"Failed to restore ASIO microphone '{device.DisplayName}'");
        }
    }
}