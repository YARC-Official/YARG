#nullable enable
using System;
using YARG.Audio.BASS;
using YARG.Core.Audio;
using YARG.Core.Logging;
using YARG.Settings;

namespace YARG.Audio.BASS.Asio
{
    /// <summary>
    ///     Represents an ASIO hardware microphone channel, managing its claimed driver input and
    ///     reconnection when the ASIO driver reinitializes.
    /// </summary>
    internal sealed class BassAsioMicSource : BassMicSourceBase
    {
        private readonly BassAsioMics _owner;
        private BassAsioInput _input;
        private float _monitoringLevel;
        private float _reverbLevel;

        public BassAsioMicSource(BassAsioMics owner, string driverId, BassAsioInput input, InputDeviceInfo info)
            : base(info.Name, info.DisplayName, info.Channel)
        {
            _owner = owner;
            DriverId = driverId;
            _input = input;
            _monitoringLevel = SettingsManager.Settings.VocalMonitoring.Value;
            _reverbLevel = SettingsManager.Settings.VocalReverb.Value;
            if (!_input.EnableMonitoring(_monitoringLevel))
            {
                YargLogger.LogWarning($"Failed to enable monitoring for ASIO microphone '{DisplayName}'");
            }
            _input.SetReverbLevel(_reverbLevel);
        }

        public string DriverId { get; }

        protected override int GetSampleRateCore() => _input.SampleRate;

        protected override bool GetIsValidCore() => _input.IsValid;

        protected override int ReadCore(Span<float> destination) => _input.Read(destination);

        protected override int GetBacklogBytesCore() => _input.GetBacklogBytes();

        protected override bool TryCreateRecordingChannelCore(bool withEffects, out int handle) =>
            _input.TryCreateRecordingChannel(withEffects, out handle);

        protected override void ReleaseRecordingChannelCore(int handle) => _input.ReleaseRecordingChannel(handle);

        protected override bool ResetToLiveCore() => _input.ResetToLive();

        protected override bool SetMonitoringLevelCore(float volume)
        {
            _monitoringLevel = volume;
            return _input.EnableMonitoring(volume);
        }

        protected override bool SetReverbLevelCore(float wet)
        {
            _reverbLevel = wet;
            return _input.SetReverbLevel(wet);
        }

        public override bool Reset()
        {
            lock (SyncRoot)
            {
                if (IsDisposed)
                {
                    return false;
                }

                return _input.Reset();
            }
        }

        protected override MicBufferInfo? GetBufferInfoCore() => _input.GetBufferInfo();

        protected override void DisposeCore()
        {
            _input.Release();
            _owner.Release(this);
        }

        internal bool Matches(string driverId, int channelIndex) =>
            string.Equals(DriverId, driverId, StringComparison.OrdinalIgnoreCase) && Channel == channelIndex;

        internal void Rebind(BassAsioInput input)
        {
            lock (SyncRoot)
            {
                if (IsDisposed)
                {
                    input.Release();
                    return;
                }

                _input.Release();
                _input = input;
                if (!_input.EnableMonitoring(_monitoringLevel))
                {
                    YargLogger.LogWarning($"Failed to restore monitoring for ASIO microphone '{DisplayName}'");
                }
                _input.SetReverbLevel(_reverbLevel);
            }

            RaiseInputChanged();
        }

        internal void Suspend()
        {
            lock (SyncRoot)
            {
                _input.Release();
            }
        }
    }
}
