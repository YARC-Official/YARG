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
        private readonly List<BassAsioMicSource> _sources = new();
        private          BassAsioOutput?         _output;

        public void Attach(BassAsioOutput output)
        {
            _output = output;
            foreach (var source in _sources)
            {
                Resume(source);
            }
        }

        public void Detach(BassAsioOutput output)
        {
            if (!ReferenceEquals(_output, output))
            {
                return;
            }

            foreach (var source in _sources)
            {
                source.Suspend();
            }

            _output = null;
        }

        public bool IsClaimed(string driverId, int channelIndex)
        {
            foreach (var source in _sources)
            {
                if (source.Matches(driverId, channelIndex))
                {
                    return true;
                }
            }

            return false;
        }

        public BassMicDevice? Create(BassAsioInput asioInput, InputDeviceInfo info)
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

            var source = new BassAsioMicSource(this, asioInput.DriverId, claimedInput, info);
            _sources.Add(source);
            return BassMicDevice.Create(source);
        }

        internal void Release(BassAsioMicSource source) => _sources.Remove(source);

        private void Resume(BassAsioMicSource source)
        {
            var output = _output;
            if (output == null || !string.Equals(source.DriverId, output.DriverId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var input = output.ClaimInput(source.DriverId, source.Channel);
            if (input != null)
            {
                try
                {
                    source.Rebind(input);
                }
                catch (Exception exception)
                {
                    YargLogger.LogException(exception, $"Failed to restore ASIO microphone '{source.DisplayName}'");
                }

                return;
            }

            YargLogger.LogWarning($"Failed to restore ASIO microphone '{source.DisplayName}'");
        }
    }
}
